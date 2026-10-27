# Diagnóstico e Solução: Erro de "Página Fora do Ar" ao Atualizar (F5) ou Acessar Rotas Diretas

## 1. Resumo Executivo

Ao navegar dentro da aplicação através de cliques em menus e botões, a navegação ocorre normalmente. No entanto, ao:
- Pressionar **F5** (Recarregar) em qualquer tela interna (ex: `/yard`, `/customers`, `/scheduling`, `/settings/...`, `/work-orders/...`);
- Ou colar a URL diretamente na barra de endereços do navegador e dar Enter;

O navegador exibe uma mensagem de **"Página fora do ar"**, **"502 Bad Gateway"** ou **"404 Not Found"**.

Este problema foi investigado minuciosamente no código-fonte e na infraestrutura de deploy do **Lavaway**. Identificamos a causa raiz exata e apresentamos as soluções detalhadas a seguir.

---

## 2. A Causa Raiz Identificada

### O Conflito no NGINX (`nginx/default.conf`)

O frontend é uma **Single Page Application (SPA)** construída em **Blazor WebAssembly** e hospedada através de um contêiner **NGINX** (`Dockerfile.frontend`).

No arquivo [`nginx/default.conf`](file:///home/rony/LPR/lavaway/nginx/default.conf), existem as seguintes diretivas de roteamento:

```nginx
# 1. Proxy padrão para chamadas de API com prefixo /api/
location /api/ {
    proxy_pass http://api_backend/;
    ...
}

# 2. Proxy direto para chamadas SEM prefixo /api/
location ~ ^/(auth|platform|identity|tenants|services|work-orders|yard|customers|scheduling|billing|loyalty|subscriptions|after-sales|settings|team-members|commission-rules)(/.*)?$ {
    proxy_pass http://api_backend;
    ...
}

# 3. Fallback do Blazor WebAssembly (SPA)
location / {
    ...
    try_files $uri $uri/ /index.html;
}
```

### Por que isso quebra o F5 e os acessos diretos?

1. **Precedência de Roteamento no NGINX:**
   O NGINX processa blocos com Expressões Regulares (`location ~`) **antes** de blocos de prefixo genérico como `location /`.
2. **Colisão de Nomes entre Rotas do Frontend e da API:**
   A regex do bloco 2 inclui palavras como:
   - `yard` (tela `/yard`)
   - `customers` (tela `/customers`)
   - `scheduling` (tela `/scheduling`)
   - `settings` (telas `/settings/team`, `/settings/services`, `/settings/profile`, etc.)
   - `work-orders` (telas `/work-orders/new`, `/work-orders/.../inspection`)
   - `after-sales` (tela `/after-sales`)
   - `loyalty` (tela `/loyalty`)
   - `subscriptions` (tela `/subscriptions`)
   - `platform` (telas `/platform/tenants`, `/platform/whatsapp`, etc.)

3. **Diferença entre Navegação Interna vs. F5:**
   - **Navegação interna (SPA):** Quando o usuário clica em um link dentro da aplicação, o Blazor WebAssembly intercepta o clique no navegador usando a *HTML5 History API* (`pushState`). O navegador não faz nenhuma requisição HTTP ao servidor NGINX. A página muda instantaneamente na memória do navegador.
   - **F5 ou Acesso Direto:** O navegador faz uma requisição HTTP `GET /yard` diretamente para o servidor NGINX.
   - **O que o NGINX faz:** O NGINX avalia a requisição e detecta que `/yard` bate com a regex `^/(...|yard|...)(/.*)?$`. Por causa da prioridade de regex, o NGINX **NÃO** entrega o `index.html` (regra `try_files`). Em vez disso, ele repassa a requisição para a API (`http://api_backend/yard`).
   - **O que a API faz:**
     - A API .NET não possui um endpoint `GET /yard` para renderizar HTML (ela é uma API REST/JSON).
     - Além disso, para endpoints que existem (como `/customers` ou `/services`), a API exige cabeçalho de autorização JWT (`Authorization: Bearer <token>`). O navegador, ao dar F5, não envia o token JWT nos cabeçalhos HTTP da navegação (o token está no `localStorage` do cliente).
     - A API responde com `404 Not Found`, `401 Unauthorized` ou `403 Forbidden` (em formato JSON).
     - Se o contêiner `api` estiver em reinicialização ou inacessível, o NGINX retorna `502 Bad Gateway`, gerando na tela do usuário a mensagem clássica de **"Página fora do ar"** ou **"Não foi possível conectar ao servidor"**.

---

## 3. Outras Possíveis Causas Secundárias

Além da causa raiz identificada no NGINX do contêiner, outros cenários de infraestrutura podem provocar o mesmo sintoma:

### A. Proxy Reverso Externo / Balanceador de Carga (Cloudflare, Traefik, NGINX Host)
Se houver outro proxy na frente do contêiner Docker (por exemplo, um NGINX instalado diretamente na VPS, Traefik, ou Nginx Proxy Manager):
- Se esse proxy externo estiver configurado para rotear arquivos estáticos sem a regra de fallback `try_files $uri $uri/ /index.html;`, ele tentará encontrar pastas físicas no disco (ex: `/var/www/html/yard`) e retornará `404 Not Found` no F5.

### B. Falha de Conexão com o Backend (`api:8080`)
Quando o NGINX tenta encaminhar requisições capturadas pela regex para `http://api_backend`, se o contêiner `api` falhar no healthcheck ou cair por falta de memória/banco de dados, o NGINX não consegue comunicar com a porta 8080 e cospe imediatamente **`502 Bad Gateway`**.

### C. Ausência de Fallback caso hospedado em Storage Estático (S3, MinIO, GitHub Pages, Azure Blob)
Em hospedagens estáticas puras, o servidor de arquivos não sabe que `/customers` é uma rota virtual do Blazor. É indispensável configurar o *Error Document* ou *Rewrite Rule* para apontar para `index.html`.

---

## 4. Soluções

### Solução 1: Remover o Bloco de Proxy Direto no NGINX (Recomendada)

No projeto [`CarWashSaaS.Client.Web`](file:///home/rony/LPR/lavaway/src/Frontend/CarWashSaaS.Client.Web/Program.cs), a aplicação já foi configurada para utilizar o prefixo `/api/` em todas as suas chamadas de backend:

```csharp
// Program.cs do Frontend
var configuredApiBaseUrl = builder.Configuration["ApiBaseUrl"]; // configurado como "/api/"
```

O bloco `location /api/ { proxy_pass http://api_backend/; }` já atende 100% das requisições de API necessárias pelo frontend. O bloco de regex direto (`location ~ ^/(auth|platform|...)`) é conflitante e redundante para uma SPA.

#### Alteração no arquivo [`nginx/default.conf`](file:///home/rony/LPR/lavaway/nginx/default.conf):

Remover (ou desativar) o bloco das linhas 71 a 82:

```diff
-    # Direct API Proxy: maps endpoints called directly without /api/ prefix
-    location ~ ^/(auth|platform|identity|tenants|services|work-orders|yard|customers|scheduling|billing|loyalty|subscriptions|after-sales|settings|team-members|commission-rules)(/.*)?$ {
-        proxy_pass http://api_backend;
-        proxy_http_version 1.1;
-        proxy_set_header Host $http_host;
-        proxy_set_header X-Real-IP $remote_addr;
-        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
-        proxy_set_header X-Forwarded-Proto $scheme;
-        proxy_buffer_size 128k;
-        proxy_buffers 4 256k;
-        proxy_busy_buffers_size 256k;
-    }
```

Dessa forma, qualquer requisição para `/yard`, `/customers`, `/settings/services`, etc. cairá no bloco `location /`:

```nginx
    # Single Page Application (SPA) fallback
    location / {
        add_header X-Frame-Options "SAMEORIGIN" always;
        add_header X-XSS-Protection "1; mode=block" always;
        add_header X-Content-Type-Options "nosniff" always;
        add_header Referrer-Policy "strict-origin-when-cross-origin" always;
        add_header Blazor-Environment "Production" always;
        try_files $uri $uri/ /index.html;
    }
```
Isso garante que ao pressionar F5, o NGINX entregará o `index.html`, o Blazor inicializará e lerá a rota atual da barra de endereços (`/yard`), renderizando a tela correta com sucesso!

---

### Solução 2: Desambiguação via Cabeçalho `Accept` (Caso ainda precise de chamadas de API sem `/api/`)

Se você possuir sistemas externos (como webhooks legados ou integrações de terceiros) que fazem requisições diretas para `/tenants`, `/services`, etc. sem o prefixo `/api/`, é possível diferenciar chamadas de página (navegador) de chamadas de API através do cabeçalho `Accept`:

Quando um usuário acessa uma página no navegador ou aperta F5, o navegador envia:
`Accept: text/html,application/xhtml+xml,...`

Quando um cliente de API chama o endpoint, ele envia:
`Accept: application/json`

No NGINX, podemos fazer o fallback para o Blazor se a requisição estiver pedindo HTML:

```nginx
    # Se for uma requisição de documento HTML feita pelo navegador (ex: F5 na tela /yard)
    # deve entregar o index.html em vez de enviar para a API.
    location ~ ^/(auth|platform|identity|tenants|services|work-orders|yard|customers|scheduling|billing|loyalty|subscriptions|after-sales|settings|team-members|commission-rules)(/.*)?$ {
        if ($http_accept ~* "text/html") {
            rewrite ^ /index.html last;
        }

        proxy_pass http://api_backend;
        proxy_http_version 1.1;
        proxy_set_header Host $http_host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_buffer_size 128k;
        proxy_buffers 4 256k;
        proxy_busy_buffers_size 256k;
    }
```

> **Nota:** A **Solução 1** é muito mais limpa, performática e aderente ao padrão da indústria (toda a API sob o prefixo `/api/` e todo o restante para o roteador SPA).

---

### Solução 3: Ajuste em Servidores Externos (Se aplicável)

Caso você utilize um NGINX externo na máquina host na frente do Docker, certifique-se de que ele repassa as URLs completas para o contêiner `web`:

```nginx
server {
    server_name app.seulavajato.com.br;

    location / {
        proxy_pass http://127.0.0.1:80; # ou a porta WEB_PORT configurada no docker-compose
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 5. Como Testar e Validar a Correção

1. Aplique a alteração no arquivo [`nginx/default.conf`](file:///home/rony/LPR/lavaway/nginx/default.conf).
2. Reconstrua o contêiner do frontend:
   ```bash
   docker compose -f docker-compose.prod.yml build web
   docker compose -f docker-compose.prod.yml up -d web
   ```
3. No navegador:
   - Acesse diretamente: `http://<seu-dominio>/yard`
   - Faça login, vá para `/customers` e aperte **F5**.
   - Vá para `/settings/services` e aperte **F5**.
4. **Resultado Esperado:** A página deve recarregar instantaneamente, restaurar o estado da aplicação e permanecer na rota solicitada sem apresentar qualquer erro de página fora do ar.
