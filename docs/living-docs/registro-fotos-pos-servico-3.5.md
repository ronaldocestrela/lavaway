# Registro de Fotos Pós-Serviço e Galeria Comparativa — Fase 3.5

## 1. Visão Geral & Objetivo

A funcionalidade **3.5 Registro de fotos pós-serviço** estabelece o registro fotográfico de **"Depois"** (*After*) para serviços de estética automotiva de alto valor agregado — com ênfase em **detalhamento (polimento técnico), vitrificação cerâmica e higienização de bancos/interiores** — associando-as diretamente às fotografias da vistoria de entrada (Fase 3.3).

O resultado é disponibilizado em uma **Galeria Comparativa Antes x Depois** interativa diretamente na Ordem de Serviço e no quadro Kanban do Pátio, fornecendo a base operacional para controle de qualidade e a base de dados necessária para o envio automatizado na **Fase 4.2** (*Notificações da OS via WhatsApp*).

---

## 2. Arquitetura Hexagonal & Fluxo de Dados

```mermaid
flowchart TD
    subgraph UI ["Frontend Blazor WebAssembly"]
        Card["YardKanbanCard.razor\n(Badge/Botão 📸 Antes/Depois)"]
        Board["YardKanbanBoard.razor & YardPage.razor"]
        GalleryComp["PhotoComparisonGallery.razor\n(Slider Interativo Split-View & Lado a Lado)"]
        ModalUpload["Modal de Captura Pós-Serviço\n(Câmera Mobile/Tablet capture='environment')"]
        Client["WorkOrderApiClient.cs"]
    end

    subgraph API ["Presentation / Minimal APIs"]
        Endpoints["YardOperationsEndpoints.cs"]
        GalleryRoute["GET /work-orders/{id}/comparison-gallery"]
        UploadRoute["POST /work-orders/{id}/post-service-photos"]
        StreamRoute["GET /work-orders/{id}/post-service-photos/{photoId}"]
        DeleteRoute["DELETE /work-orders/{id}/post-service-photos/{photoId}"]
    end

    subgraph App ["Application Layer"]
        AppService["WorkOrderApplicationService.cs"]
        Rule["PostServiceEligibilityRule.cs"]
    end

    subgraph Domain ["YardOperations Domain (DDD Puro)"]
        WO["WorkOrder (Aggregate Root)"]
        PhotoEntity["PostServicePhoto (Entity com IMustHaveTenant)"]
        Inspection["VehicleInspection (Entrada)"]
    end

    subgraph Storage ["Storage & Persistência"]
        MinIO[(MinIO Object Storage\ntenants/{tenantId}/post-service-photos/...)]
        SQL[(SQL Server - schema yard\nWorkOrderPostServicePhotos)]
    end

    Card -->|Clique em 'Antes/Depois'| Board
    Board --> GalleryComp
    GalleryComp --> ModalUpload
    GalleryComp & ModalUpload --> Client
    Client --> GalleryRoute & UploadRoute & StreamRoute & DeleteRoute
    GalleryRoute & UploadRoute & StreamRoute & DeleteRoute --> AppService
    AppService --> Rule
    AppService --> WO
    WO --> PhotoEntity
    AppService --> Inspection
    AppService --> MinIO
    AppService --> SQL
```

---

## 3. Regras de Negócio e Invariantes do Domínio

1. **Elegibilidade de Serviços (`PostServiceEligibilityRule`):**
   * Serviços são avaliados automaticamente com base na categoria cadastrada e em palavras-chave no nome do serviço:
     * **Detalhamento / Polimento:** Categoria `Polimento` ou nome contendo `"detalhamento"`, `"polimento"`, `"detail"`, `"cristalização"`.
     * **Vitrificação:** Categoria `Vitrificação` ou nome contendo `"vitrificação"`, `"vitrificacao"`, `"ceramic"`, `"coating"`.
     * **Bancos / Interiores:** Categoria `Higienização` ou nome contendo `"banco"`, `"estofado"`, `"couro"`, `"higienização"`.
   * Tentativas de registrar fotos em OSs sem serviços elegíveis falham com `work_order.no_eligible_services`.
   * Tentativas de associar uma foto a um item de serviço não elegível falham com `work_order.item_not_eligible`.

2. **Janela Operacional Permitida:**
   * O registro de fotos de "Depois" é bloqueado enquanto o veículo estiver no status inicial `Waiting` (`work_order.invalid_status_for_post_service_photos`), sendo liberado a partir do início da lavagem (`InWashing`), acabamento (`Finishing`), controle de qualidade (`QualityControl`) e liberação (`ReadyForPickup`).

3. **Pareamento com Fotos de Entrada:**
   * Cada foto de "Depois" pode ser vinculada a um `BeforeInspectionPhotoId` específico da vistoria inicial ou à mesma categoria de perímetro (`InspectionPhotoCategory`).
   * A galeria consolida automaticamente os pares correspondentes (`ComparisonPairs`), mantendo também o inventário de fotos avulsas da entrada e do pós-serviço.

4. **Multi-Tenancy Estrito e Isolamento de Mídia:**
   * A entidade `PostServicePhoto` implementa compulsoriamente `IMustHaveTenant` e chave primária sequencial `Guid.CreateVersion7()`.
   * O armazenamento no MinIO é segregado sob `tenants/{tenantId}/post-service-photos/{uniqueFileName}` via `ITenantObjectStorage`.
   * Tentativas de leitura, streaming ou mutação entre diferentes estabelecimentos retornam compulsoriamente `404 Not Found`.

5. **Formatos e Tamanhos:**
   * Suporte a formatos de imagem `image/jpeg`, `image/png` e `image/webp`.
   * Limite de tamanho: entre 1 byte e 10 MB por fotografia.

---

## 4. Contratos Compartilhados (`CarWashSaaS.Shared.Contracts`)

* **`PostServicePhotoDto`:**
  * `Id`: identificador único UUIDv7 da foto pós-serviço.
  * `WorkOrderId`: identificador da OS vinculada.
  * `WorkOrderItemId`: serviço associado (opcional).
  * `ServiceName`: nome do serviço associado para exibição.
  * `BeforeInspectionPhotoId`: identificador da foto de entrada pareada (opcional).
  * `Category`: categoria de perímetro/área (`InspectionPhotoCategory`).
  * `Title`: título descritivo do acabamento.
  * `FileName`, `ContentType`, `SizeBytes`: metadados do arquivo.
  * `UploadedAtUtc`: data e hora UTC do envio.
  * `Notes`: observações técnicas e produtos aplicados.

* **`PostServiceComparisonPairDto`:**
  * Representa o par antes/depois (`BeforePhoto` + `AfterPhoto`), com nome do serviço, categoria e observações.

* **`WorkOrderComparisonGalleryDto`:**
  * `WorkOrderId`, `CustomerName`, `Plate`, `VehicleSize`, `Status`, `IsEligible`.
  * `EligibleServices`: lista de serviços elegíveis da OS.
  * `ComparisonPairs`: lista de pares pareados com antes e depois.
  * `UnpairedBeforePhotos`: fotos de entrada disponíveis para pareamento.
  * `UnpairedAfterPhotos`: fotos pós-serviço avulsas.

---

## 5. Endpoints REST (Minimal APIs)

| Método | Rota | Autorização | Finalidade |
| :--- | :--- | :--- | :--- |
| `GET` | `/work-orders/{id}/comparison-gallery` | `ViewCustomers` | Retorna a visão consolidada da galeria comparativa da OS. |
| `POST` | `/work-orders/{id}/post-service-photos` | `UpdateWorkOrderStatus` | Upload multipart da foto de "Depois" com metadados e pareamento. |
| `GET` | `/work-orders/{id}/post-service-photos/{photoId}` | `ViewCustomers` | Stream da imagem com isolamento de tenant e Content-Type. |
| `DELETE` | `/work-orders/{id}/post-service-photos/{photoId}` | `UpdateWorkOrderStatus` | Remove uma foto pós-serviço existente. |

---

## 6. Especificações Executáveis (BDD / Gherkin)

### Cenário 1: Pareamento e visualização com sucesso de foto pós-serviço
* **Dado** que uma Ordem de Serviço possui o serviço de "Vitrificação 9H" e foi realizada a vistoria de entrada com foto dianteira
* **E** a OS avançou para a etapa de "Secagem / Acabamento" (*Finishing*)
* **Quando** o operador envia a foto de "Depois" vinculando-a à foto de entrada
* **Então** a foto é armazenada no MinIO sob o tenant correspondente
* **E** a galeria comparativa exibe o par Antes x Depois com o controle deslizante (slider) ativo.

### Cenário 2: Rejeição de foto pós-serviço para OS sem serviços elegíveis
* **Dado** que uma Ordem de Serviço possui unicamente o serviço de "Ducha Simples"
* **Quando** o atendente tenta enviar uma foto pós-serviço
* **Então** o sistema rejeita a operação com erro de validação `work_order.no_eligible_services`
* **E** nenhuma alteração é persistida.

### Cenário 3: Bloqueio de acesso cross-tenant
* **Dado** que o Lava-Jato A registrou fotos pós-serviço em sua OS
* **Quando** um usuário autenticado no Lava-Jato B tenta consultar a galeria ou baixar o stream da imagem
* **Então** o sistema retorna `404 Not Found`
* **E** nenhum arquivo ou dado do Lava-Jato A é exposto.
