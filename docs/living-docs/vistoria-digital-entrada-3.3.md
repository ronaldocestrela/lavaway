# Vistoria Digital de Entrada — Fase 3.3

## Objetivo e Estado

A vistoria digital de entrada conclui o processo de recepção física do veículo no lava-jato ou centro de estética automotiva. Ela vincula à Ordem de Serviço recém-aberta ([WorkOrder](file:///home/rony/LPR/lavaway/src/Backend/Modules/YardOperations/CarWashSaaS.YardOperations.Domain/WorkOrder.cs)) o mapeamento fotográfico e vetorial do estado do veículo, resguardando o estabelecimento e o proprietário contra contestações de danos pré-existentes.

A implementação abrange de ponta a ponta:
- **Backend:** Agregado puro `VehicleInspection` com entidades `InspectionDamage`, `InspectionChecklistItem` e `InspectionPhoto`, isolamento multi-tenant estrito via `IMustHaveTenant` e Global Query Filters, persistência no schema `yard` com SQL Server, storage privado no MinIO sob `tenants/{tenantId}/inspections/{fileName}` e endpoints REST Minimal API autenticados.
- **Frontend Blazor:** Interface responsiva para celulares e tablets com componentes isolados (`VehicleInspectionDiagram` com SVG interativo de 5 vistas, `InspectionChecklistCard` com nível de combustível e odômetro tátil, `InspectionPhotoGallery` com disparador de câmera nativa de smartphone para as 4 fotos obrigatórias de perímetro) e a página orquestradora `InspectionPage` (`/work-orders/{id}/inspection`), integrada ao fluxo de check-in (`WorkOrderCreatedModal`).
- **Qualidade:** 100% de cobertura com testes de unidade TDD, testes de arquitetura (NetArchTest), testes de integração multi-tenant e testes de componentes bUnit.

---

## Fluxo Operacional da Vistoria

```mermaid
flowchart TD
    OS[OS Aberta / WorkOrderCreatedModal] -->|Clique em 'Fazer Vistoria'| PAGE[InspectionPage /work-orders/{id}/inspection]
    PAGE --> GET[GET /work-orders/{id}/inspection]
    GET -->|Cria ou Carrega Rascunho| DRAFT[VehicleInspection Status: Draft]
    
    subgraph Execucao["Conferência de Pátio no Celular / Tablet"]
        DRAFT --> TAB1[1. Diagrama de Avarias\nToque no SVG para Marcar Arranhões/Mossas/Trincas]
        DRAFT --> TAB2[2. Checklist & Combustível\nEstepe, Pertences, Luzes Painel, Odômetro]
        DRAFT --> TAB3[3. Fotos Obrigatórias de Perímetro\nFrente, Traseira, Lateral Esq., Lateral Dir.]
    end

    TAB1 -->|POST /damages| DAMAGE_API[Grava Avaria com Coordenadas X%, Y%]
    TAB2 -->|PUT /checklist| CHECKLIST_API[Salva Conferência de Pertences]
    TAB3 -->|POST /photos multipart| PHOTO_API[Armazena Foto no MinIO e Associa à Vistoria]

    PHOTO_API --> CHECK_PHOTOS{As 4 Fotos Obrigatórias\nForam Enviadas?}
    CHECK_PHOTOS -->|Não| LOCK_BTN[Botão 'Concluir Vistoria' Desabilitado]
    CHECK_PHOTOS -->|Sim| UNLOCK_BTN[Botão 'Concluir Vistoria' Habilitado]

    UNLOCK_BTN -->|POST /complete| COMPLETE[Conclui Vistoria / Status: Completed]
    COMPLETE --> IMMUTABLE[Vistoria Imutável e Bloqueada contra Alterações]
    COMPLETE --> READY[Veículo Pronto para o Fluxo Operacional / Kanban 3.4]
```

---

## Regras de Negócio e Invariantes

1. **Agregados e Entidades de Domínio:** `VehicleInspection`, `InspectionDamage`, `InspectionChecklistItem` e `InspectionPhoto` pertencem ao módulo `YardOperations`; todos implementam `IMustHaveTenant` e utilizam identificadores `Guid` sequenciais (`Guid.CreateVersion7()`).
2. **Vinculação à Ordem de Serviço:** Uma vistoria pertence obrigatoriamente a uma `WorkOrder` existente do mesmo `TenantId`. Tentativas de acesso cross-tenant retornam `404 Not Found`.
3. **Regra das 4 Fotos Obrigatórias de Perímetro:** O método de negócio `inspection.Complete()` valida compulsoriamente a presença de fotos para as 4 categorias essenciais:
   - `InspectionPhotoCategory.Front` (Frente e Placa Dianteira)
   - `InspectionPhotoCategory.Rear` (Traseira e Placa Traseira)
   - `InspectionPhotoCategory.LeftSide` (Lateral Esquerda completa)
   - `InspectionPhotoCategory.RightSide` (Lateral Direita completa)
   Caso qualquer uma esteja ausente, o comando falha com `ErrorType.Validation` (`inspection.required_photos_missing`).
4. **Coordenadas Normalizadas e Responsividade:** As avarias marcadas no diagrama vetorial registram coordenadas percentuais (`CoordinateX` e `CoordinateY` de 0.00% a 100.00%), garantindo que a renderização dos pinos permaneça milimetricamente exata em qualquer tela (smartphones, tablets de pátio ou monitores de recepção).
5. **Tipos e Severidade de Avarias:** Suporte aos tipos `Scratch` (Arranhão), `Dent` (Mossa/Amassado), `Crack` (Trinca de Vidro), `PaintChip` (Picado de Pedra na Pintura) e `Other`, com severidades `Low`, `Medium` e `High` para alertas visuais distintos.
6. **Imutabilidade pós-conclusão:** Após concluída (`Completed`), a vistoria torna-se somente-leitura, impedindo exclusão ou inserção posterior de dados.
7. **Isolamento de Storage MinIO:** O armazenamento das imagens ocorre no bucket privado via `ITenantObjectStorage` sob o namespace estrito `tenants/{tenantId}/inspections/{fileName}`, impedindo vazamento de mídia entre diferentes lava-jatos.

---

## Endpoints REST (Minimal APIs)

- `GET /work-orders/{workOrderId}/inspection`: Retorna os detalhes completos da vistoria (`VehicleInspectionDto`).
- `POST /work-orders/{workOrderId}/inspection`: Cria ou recupera o rascunho da vistoria para a OS informada (`CreateInspectionRequest`).
- `PUT /work-orders/{workOrderId}/inspection/checklist`: Atualiza nível de combustível, odômetro, observações e itens de conferência (`UpdateInspectionChecklistRequest`).
- `POST /work-orders/{workOrderId}/inspection/damages`: Registra avaria com coordenadas no diagrama (`AddInspectionDamageRequest`).
- `DELETE /work-orders/{workOrderId}/inspection/damages/{damageId}`: Remove uma avaria marcada.
- `POST /work-orders/{workOrderId}/inspection/photos`: Upload multipart de foto com categoria e associação opcional a avaria.
- `GET /work-orders/{workOrderId}/inspection/photos/{photoId}`: Stream da foto com isolamento multi-tenant e Content-Type correto.
- `POST /work-orders/{workOrderId}/inspection/complete`: Valida fotos obrigatórias e conclui a vistoria.

---

## Componentes Blazor

- `VehicleInspectionDiagram.razor` (`CarWashSaaS.Client.Components`): Diagrama SVG interativo de 5 vistas (Superior, Lateral Esquerda, Lateral Direita, Frente, Traseira) com toque na lataria/vidros, modal de seleção de tipo/severidade e pinos pulsantes.
- `InspectionChecklistCard.razor` (`CarWashSaaS.Client.Components`): Seletor de combustível tipo pill (Reserva a Cheio), input numérico de odômetro e lista de itens rápidos (estepe, macaco, pertences, tapetes, painel).
- `InspectionPhotoGallery.razor` (`CarWashSaaS.Client.Components`): Grade tátil com 4 slots obrigatórios destacados, suporte a disparo de câmera nativa de smartphone (`capture="environment"`), preview imediato e galeria de fotos adicionais.
- `WorkOrderCreatedModal.razor` (`CarWashSaaS.Client.Components`): Modal de sucesso pós-check-in com novo botão em destaque "Fazer Vistoria".
- `InspectionPage.razor` (`CarWashSaaS.Client.Web`): Página orquestradora em `/work-orders/{workOrderId}/inspection` com abas ágeis e barra fixa inferior de conclusão.

---

## Especificações Executáveis (BDD / Cenários de Teste)

### Cenário 1: Bloqueio de Conclusão da Vistoria com Fotos Faltantes
- **Dado** que uma Ordem de Serviço possui uma vistoria em andamento
- **E** foram registradas apenas 2 fotos de perímetro (Frente e Traseira)
- **Quando** o atendente tenta concluir a vistoria
- **Então** o sistema rejeita a operação com erro de validação `inspection.required_photos_missing`
- **E** a vistoria permanece com status `Draft`.

### Cenário 2: Conclusão com Sucesso ao Fornecer as 4 Fotos de Perímetro
- **Dado** que a vistoria possui fotos registradas para Frente, Traseira, Lateral Esquerda e Lateral Direita
- **Quando** o atendente solicita a conclusão
- **Então** o status é atualizado para `Completed`
- **E** a data de conclusão `CompletedAtUtc` é preenchida
- **E** modificações subsequentes no diagrama ou checklist são bloqueadas.

### Cenário 3: Isolamento Multi-Tenant em Fotos e Dados de Vistoria
- **Dado** que o Lava-Jato A registrou uma vistoria e fotos para um veículo
- **Quando** um usuário autenticado no Lava-Jato B tenta consultar ou baixar a foto dessa vistoria
- **Então** o sistema retorna `404 Not Found`
- **E** nenhum dado ou imagem do Lava-Jato A é exposto.
