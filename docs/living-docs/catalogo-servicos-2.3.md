# Catálogo de serviços e preços — Fase 2.3

## Objetivo
Permitir que o gestor cadastre, consulte e atualize os serviços oferecidos pelo estabelecimento, com preço e duração estimada por porte de veículo.

## Fluxo operacional

```mermaid
flowchart LR
    A[Gestor autenticado] --> B[Consulta catálogo]
    B --> C{Existem serviços?}
    C -->|Sim| D[Lista serviços por tenant]
    C -->|Não| E[Cria novo serviço]
    E --> F[Valida tenant, categoria e preços]
    D --> G[Seleciona serviço para edição]
    G --> F
    F --> H[Service.Create / Update]
    H --> I[Persistência no YardOperationsDbContext]
    I --> J[Retorna Result<T>]
    J --> K[API responde 200/201/400/404/409]
```

## Regras da implementação

- `Service` é o agregado principal do módulo `YardOperations`.
- Cada serviço possui uma categoria e pelo menos um preço por porte de veículo.
- `ServicePrice` guarda `VehicleSize`, `Amount` e `EstimatedDurationMinutes`.
- A regra de negócio proíbe mais de um preço para o mesmo `VehicleSize` no mesmo serviço.
- O `TenantId` do serviço é obrigatório e protegido por filtro global do módulo.
- O `Result<T>` encapsula falhas de validação, not found e conflitos.

## Entregáveis desta subfase

- `Service` e `ServicePrice` no domínio
- `CreateServiceCommand` e `UpdateServiceCommand`
- `ServiceCatalogApplicationService`
- `IServiceRepository` e `ServiceRepository`
- endpoints `GET /services`, `GET /services/{id}`, `POST /services` e `PUT /services/{id}`
- autorização de administrador e isolamento por tenant
- testes unitários cobrindo criação e rejeição de preços duplicados
