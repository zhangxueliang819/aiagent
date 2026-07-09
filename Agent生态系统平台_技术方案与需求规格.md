


# Agent生态系统平台 - 技术方案与需求规格

## 1. 项目概述

构建一个基于 C# 模块化单体架构的 Agent 生态系统平台，以 ASP.NET Core 为核心框架，采用混合存储策略（关系型数据库 + Redis + 向量数据库），提供 Agent 全生命周期管理、多模型集成、核心组件管理及平台基础服务能力。

## 2. 技术栈

| 层级 | 技术选型 | 说明 |
|------|---------|------|
| 运行时 | .NET 8 | LTS版本，支持AOT编译 |
| Web框架 | ASP.NET Core 8 | RESTful API + Minimal API |
| ORM | Entity Framework Core 8 | 支持多数据库提供程序 |
| 关系数据库 | PostgreSQL 15+ | 主业务数据存储 |
| 缓存 | Redis 7+ | 会话缓存、短期记忆、速率限制 |
| 向量数据库 | pgvector (PostgreSQL扩展) / Qdrant | 长期记忆的向量检索 |
| 消息队列 | Kafka | 异步任务处理 |
| 认证授权 | ASP.NET Core Identity + JWT | 身份验证和授权 |
| API文档 | Swagger / Scalar | OpenAPI规范 |
| 日志 | Serilog + Elasticsearch/Splunk | 结构化日志和审计 |
| 监控 | Prometheus + Grafana / Application Insights | 性能监控 |
| 容器化 | Docker + Docker Compose | 开发和测试环境 |
| 测试 | xUnit + Moq + Testcontainers | 单元测试和集成测试 |
| **前端框架** | **Vue 3 + TypeScript** | **组合式API + 脚本增强** |
| **构建工具** | **Vite 5+** | **快速HMR和构建** |
| **UI组件库** | **Element Plus** | **企业级组件和主题定制** |
| **状态管理** | **Pinia** | **TypeScript优先的状态管理** |
| **路由** | **Vue Router 4** | **路由守卫和懒加载** |
| **HTTP请求** | **Axios + HTTP拦截器** | **请求/响应拦截、Token刷新** |
| **实时通信** | **SignalR Client** | **流式对话、实时通知** |
| **图表** | **ECharts + Vue-ECharts** | **仪表盘和统计图表** |
| **表单验证** | **VeeValidate + Zod** | **声明式表单验证** |
| **CSS方案** | **SCSS + CSS Variables** | **主题切换和样式管理** |
| **国际化** | **Vue I18n** | **多语言支持** |
| **测试** | **Vitest + Vue Test Utils** | **前端单元测试** |

## 3. 项目结构（模块化单体架构）

```
AgentPlatform.sln
├── src/
│   ├── AgentPlatform.Web/                    # ASP.NET Core 启动项目
│   │   ├── Controllers/                      # RESTful API 控制器
│   │   ├── Middleware/                        # 中间件（认证、日志、审计）
│   │   ├── Hubs/                             # SignalR Hub（实时通信）
│   │   ├── BackgroundServices/               # 后台任务服务
│   │   └── Program.cs
│   │
│   ├── AgentPlatform.Core/                   # 核心领域模型和接口
│   │   ├── Models/                           # 领域实体
│   │   ├── Enums/                            # 枚举定义
│   │   ├── Interfaces/                       # 核心接口定义
│   │   └── Exceptions/                       # 自定义异常
│   │
│   ├── AgentPlatform.Application/            # 应用层/用例层
│   │   ├── Services/                         # 应用服务
│   │   ├── DTOs/                             # 数据传输对象
│   │   ├── Validators/                       # FluentValidation 验证
│   │   └── Mappings/                         # AutoMapper 配置
│   │
│   ├── AgentPlatform.Infrastructure/         # 基础设施层
│   │   ├── Persistence/                      # EF Core DbContext + 仓储实现
│   │   ├── Repositories/                     # 仓储实现
│   │   ├── Cache/                            # Redis 缓存服务
│   │   ├── VectorStore/                      # 向量数据库客户端
│   │   ├── MessageQueue/                     # 消息队列客户端
│   │   └── Logging/                          # 日志实现
│   │
│   ├── AgentPlatform.AgentEngine/            # Agent 核心引擎
│   │   ├── AgentManager.cs                   # Agent 生命周期管理器
│   │   ├── AgentExecutor.cs                  # Agent 执行引擎
│   │   ├── SkillRegistry.cs                  # 技能注册表
│   │   ├── McpHandler.cs                     # MCP 处理器
│   │   └── AgentStateMachine.cs              # Agent 状态机
│   │
│   ├── AgentPlatform.ModelProviders/        # 模型提供程序
│   │   ├── IModelProvider.cs                 # 模型提供程序接口
│   │   ├── OpenAIProvider.cs                 # OpenAI 兼容实现
│   │   ├── ModelRouter.cs                    # 模型路由/负载均衡
│   │   └── ModelMetricsCollector.cs          # 模型性能指标收集
│   │
│   ├── AgentPlatform.Memory/                 # 记忆管理模块
│   │   ├── ShortTermMemoryStore.cs           # 短期记忆（Redis）
│   │   ├── LongTermMemoryStore.cs            # 长期记忆（向量数据库）
│   │   ├── MemoryRetrievalService.cs         # 记忆检索服务
│   │   └── MemoryConsolidationService.cs     # 记忆整合服务
│   │
│   ├── AgentPlatform.Session/                # 会话管理模块
│   │   ├── SessionManager.cs                 # 会话管理器
│   │   ├── SessionStore.cs                   # 会话存储
│   │   └── ConversationContext.cs            # 对话上下文
│   │
│   └── AgentPlatform.Billing/               # 计费统计模块
│       ├── UsageTracker.cs                   # 使用量跟踪
│       ├── BillingService.cs                 # 计费服务
│       └── RateLimiter.cs                    # 速率限制
│
├── ui/                                       # 前端项目
│   ├── agent-platform-admin/                 # Vue 3 管理后台
│   │   ├── src/
│   │   │   ├── api/                          # API接口层
│   │   │   ├── assets/                       # 静态资源
│   │   │   ├── components/                   # 公共组件
│   │   │   ├── composables/                  # 组合式函数
│   │   │   ├── layouts/                      # 布局组件
│   │   │   ├── locales/                      # 国际化语言包
│   │   │   ├── router/                       # 路由配置
│   │   │   ├── stores/                       # Pinia状态仓库
│   │   │   ├── styles/                       # 全局样式
│   │   │   ├── types/                        # TypeScript类型定义
│   │   │   ├── utils/                        # 工具函数
│   │   │   ├── views/                        # 页面视图
│   │   │   ├── App.vue
│   │   │   └── main.ts
│   │   ├── public/
│   │   ├── index.html
│   │   ├── vite.config.ts
│   │   ├── tsconfig.json
│   │   ├── package.json
│   │   └── env.d.ts
│   │
│   ├── agent-platform-chat/                  # Vue 3 对话界面
│   │   └── src/
│   │       ├── api/
│   │       ├── components/                   # 对话专用组件
│   │       ├── composables/
│   │       ├── router/
│   │       ├── stores/
│   │       ├── types/
│   │       ├── views/
│   │       ├── App.vue
│   │       └── main.ts
│   │   ├── index.html
│   │   ├── vite.config.ts
│   │   ├── package.json
│   │   └── tsconfig.json
│   │
│   └── shared/                               # 前后端共享类型
│       ├── types/                            # 共享 TypeScript 类型
│       └── api-contracts/                    # API 契约定义
│
├── tests/
│   ├── AgentPlatform.UnitTests/              # 单元测试
│   ├── AgentPlatform.IntegrationTests/       # 集成测试
│   └── AgentPlatform.ArchitectureTests/      # 架构测试（NetArchTest）
│
└── docs/
    ├── api/                                  # OpenAPI 规范
    └── sql/                                  # 数据库迁移脚本
```

## 4. 功能模块设计

### 4.1 Agent 生命周期管理

**核心实体:**
- `AgentDefinition` - Agent 定义（名称、描述、系统提示词、模型配置）
- `AgentInstance` - Agent 运行实例（状态、当前会话、运行时指标）
- `AgentVersion` - Agent 版本管理
- `AgentConfiguration` - Agent 配置（参数、技能绑定、MCP端点）

**状态机:** `Draft -> Active -> Running -> Paused -> Stopped -> Archived`

**健康检查:** 定时心跳检测 + 响应延迟检测 + 模型可用性检测

### 4.2 多模型集成

**模型提供程序接口 `IModelProvider`:**
```csharp
public interface IModelProvider
{
    string ProviderName { get; }
    Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken ct);
    Task<IAsyncEnumerable<string>> CompleteStreamAsync(ChatCompletionRequest request, CancellationToken ct);
    Task<ModelInfo> GetModelInfoAsync(string modelId);
    Task<bool> HealthCheckAsync();
}
```

**模型路由策略:** 支持轮询(RoundRobin)、最少连接(LeastConnections)、权重(Weighted)三种负载均衡策略。

**模型配置数据:**
- 端点 URL / API Key (加密存储)
- 模型 ID (gpt-4o, gpt-4o-mini 等)
- 速率限制 (RPM, TPM)
- 权重和优先级
- 健康状态

### 4.3 技能系统

**技能接口 `ISkill`:**
```csharp
public interface ISkill
{
    string Name { get; }
    string Description { get; }
    SkillType Type { get; }
    Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct);
    SkillSchema GetInputSchema();
}
```

**技能注册:** 通过依赖注入自动注册，支持 `IServiceCollection.AddSkill<T>()` 扩展方法。

**技能类型:**
- 内置技能 (代码执行、文件操作、网络搜索)
- 自定义技能 (通过 API 注册)
- MCP 技能 (通过 MCP 协议暴露)

### 4.4 MCP (Model Context Protocol) 集成

- MCP 端点管理：注册、健康检查、自动重连
- 请求/响应协议适配器
- 工具发现和缓存
- 资源上下文管理

### 4.5 记忆管理系统

**短期记忆 (Redis):**
- 基于会话的最近 N 轮对话
- TTL 自动过期
- 序列化为 JSON

**长期记忆 (向量数据库):**
- 对话摘要向量化
- 语义相似度检索
- 记忆分层：会话级 -> 用户级 -> Agent级
- 定期记忆整合与压缩

### 4.6 会话管理

- 多用户并发会话支持
- 会话状态持久化
- 会话超时自动回收
- 上下文窗口管理 (Token 计数和截断策略)

### 4.7 平台服务

**RESTful API:**
- /api/v1/agents - Agent 管理
- /api/v1/chat - 对话接口
- /api/v1/skills - 技能管理
- /api/v1/models - 模型管理
- /api/v1/sessions - 会话管理
- /api/v1/usage - 使用量统计
- /api/v1/health - 健康检查

**认证授权:**
- API Key + JWT 两种认证方式
- 基于角色的访问控制 (RBAC)
- 细粒度权限: Agent 级别、技能级别

## 5. 数据库设计

### 5.1 关系数据库 (PostgreSQL/SQL Server)

```sql
-- Agents 表
CREATE TABLE Agents (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name            NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(2000),
    SystemPrompt    NTEXT,
    Status          TINYINT NOT NULL DEFAULT 0, -- Draft=0,Active=1,Paused=2,Archived=3
    OwnerId         UNIQUEIDENTIFIER NOT NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Version         INT NOT NULL DEFAULT 1,
    IsDeleted       BIT NOT NULL DEFAULT 0
);

-- Agent Versions 表
CREATE TABLE AgentVersions (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AgentId         UNIQUEIDENTIFIER NOT NULL REFERENCES Agents(Id),
    VersionNumber   INT NOT NULL,
    SystemPrompt    NTEXT,
    ConfigurationJson NTEXT,  -- JSON 格式的完整配置快照
    Changelog       NVARCHAR(2000),
    CreatedBy       UNIQUEIDENTIFIER NOT NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Agent Configurations 表
CREATE TABLE AgentConfigurations (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AgentId         UNIQUEIDENTIFIER NOT NULL REFERENCES Agents(Id),
    ModelProviderId UNIQUEIDENTIFIER NOT NULL,
    ModelId         NVARCHAR(100) NOT NULL,
    Temperature     FLOAT DEFAULT 0.7,
    MaxTokens       INT DEFAULT 4096,
    TopP            FLOAT DEFAULT 1.0,
    FrequencyPenalty FLOAT DEFAULT 0.0,
    PresencePenalty FLOAT DEFAULT 0.0,
    ContextWindowSize INT DEFAULT 8192,
    MaxRetries      INT DEFAULT 3,
    TimeoutSeconds  INT DEFAULT 60
);

-- Model Providers 表
CREATE TABLE ModelProviders (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProviderName    NVARCHAR(100) NOT NULL, -- OpenAI, AzureOpenAI, Custom
    DisplayName     NVARCHAR(200),
    ApiEndpoint     NVARCHAR(500) NOT NULL,
    ApiKeyEncrypted NVARCHAR(500),  -- 加密存储
    IsEnabled       BIT NOT NULL DEFAULT 1,
    Priority        INT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Model Endpoints 表 (负载均衡)
CREATE TABLE ModelEndpoints (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProviderId      UNIQUEIDENTIFIER NOT NULL REFERENCES ModelProviders(Id),
    ModelId         NVARCHAR(100) NOT NULL, -- gpt-4o, gpt-4o-mini
    DeploymentName  NVARCHAR(200),
    EndpointUrl     NVARCHAR(500) NOT NULL,
    Weight          INT NOT NULL DEFAULT 1,
    RpmLimit        INT,  -- 每分钟请求限制
    TpmLimit        INT,  -- 每分钟Token限制
    IsActive        BIT NOT NULL DEFAULT 1,
    LastHealthCheck DATETIME2,
    HealthStatus    TINYINT DEFAULT 1
);

-- Skills 表
CREATE TABLE Skills (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name            NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(2000),
    SkillType       TINYINT NOT NULL, -- BuiltIn=0, Custom=1, MCP=2
    AssemblyName    NVARCHAR(500),    -- 内置技能的程序集
    EndpointUrl     NVARCHAR(500),    -- 自定义/MCP技能的端点
    InputSchemaJson NTEXT,            -- JSON Schema
    IsEnabled       BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Agent Skills 关联表
CREATE TABLE AgentSkills (
    AgentId         UNIQUEIDENTIFIER NOT NULL REFERENCES Agents(Id),
    SkillId         UNIQUEIDENTIFIER NOT NULL REFERENCES Skills(Id),
    ExecutionOrder  INT NOT NULL DEFAULT 0,
    Parameters      NVARCHAR(MAX), -- JSON 参数覆盖
    PRIMARY KEY (AgentId, SkillId)
);

-- MCP Endpoints 表
CREATE TABLE McpEndpoints (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name            NVARCHAR(200) NOT NULL,
    EndpointUrl     NVARCHAR(500) NOT NULL,
    Protocol        TINYINT NOT NULL DEFAULT 0, -- SSE=0, StreamableHTTP=1
    AuthType        TINYINT DEFAULT 0, -- None=0, ApiKey=1, Bearer=2
    AuthCredentials NVARCHAR(500),  -- 加密存储
    IsEnabled       BIT NOT NULL DEFAULT 1,
    LastSyncAt      DATETIME2,
    ToolCacheJson   NTEXT           -- 缓存的工具列表
);

-- Sessions 表
CREATE TABLE Sessions (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AgentId         UNIQUEIDENTIFIER NOT NULL REFERENCES Agents(Id),
    UserId          NVARCHAR(200),
    ExternalUserId  NVARCHAR(200),
    Status          TINYINT NOT NULL DEFAULT 0, -- Active=0, Paused=1, Closed=2
    TokenUsage      INT DEFAULT 0,
    MessageCount    INT DEFAULT 0,
    StartedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastActivityAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt       DATETIME2,
    MetadataJson    NTEXT
);

-- Conversations (消息历史)
CREATE TABLE Conversations (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    SessionId       UNIQUEIDENTIFIER NOT NULL REFERENCES Sessions(Id),
    Role            TINYINT NOT NULL, -- User=0, Assistant=1, System=2, Tool=3
    Content         NTEXT NOT NULL,
    ToolCallId      NVARCHAR(100),
    ToolName        NVARCHAR(200),
    TokenCount      INT DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Usage Tracking 表
CREATE TABLE UsageRecords (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AgentId         UNIQUEIDENTIFIER NOT NULL REFERENCES Agents(Id),
    SessionId       UNIQUEIDENTIFIER REFERENCES Sessions(Id),
    ModelProviderId UNIQUEIDENTIFIER REFERENCES ModelProviders(Id),
    ModelId         NVARCHAR(100),
    PromptTokens    INT NOT NULL DEFAULT 0,
    CompletionTokens INT NOT NULL DEFAULT 0,
    TotalTokens     INT NOT NULL DEFAULT 0,
    RequestCost     DECIMAL(18,6),
    DurationMs      INT,
    Status          TINYINT NOT NULL DEFAULT 0, -- Success=0, Failed=1, Partial=2
    ErrorMessage    NVARCHAR(2000),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Audit Logs 表
CREATE TABLE AuditLogs (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER,
    Action          NVARCHAR(200) NOT NULL,
    EntityType      NVARCHAR(100),
    EntityId        NVARCHAR(100),
    OldValue        NVARCHAR(MAX),
    NewValue        NVARCHAR(MAX),
    IpAddress       NVARCHAR(50),
    UserAgent       NVARCHAR(500),
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- API Keys 表
CREATE TABLE ApiKeys (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    KeyHash         NVARCHAR(200) NOT NULL,  -- SHA256 hash
    KeyPrefix       NVARCHAR(10) NOT NULL,   -- 前8位明文，方便识别
    Name            NVARCHAR(200),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    Permissions     NVARCHAR(MAX),           -- JSON 权限列表
    RateLimit       INT DEFAULT 1000,        -- 每小时请求限制
    IsEnabled       BIT NOT NULL DEFAULT 1,
    ExpiresAt       DATETIME2,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastUsedAt      DATETIME2
);
```

### 5.2 Redis 缓存设计

| Key 模式 | 说明 | TTL |
|---------|------|-----|
| `session:{id}:messages` | 最近N轮对话消息 (List) | 会话有效期 |
| `session:{id}:state` | 会话状态 (Hash) | 会话有效期 |
| `agent:{id}:status` | Agent运行状态 | 30s (心跳更新) |
| `rate_limit:{key}:{window}` | API速率限制计数器 (SortedSet) | 窗口期 |
| `memory:short:{sessionId}` | 短期记忆缓存 | 1h |
| `cache:model:{id}:info` | 模型信息缓存 | 5min |
| `cache:skill:list` | 技能列表缓存 | 1min |

### 5.3 向量数据库 (pgvector/Qdrant)

**Collection: `agent_memories`**
```json
{
  "id": "uuid",
  "vector": [0.1, 0.2, ...],  // 1536维 embedding
  "payload": {
    "agentId": "uuid",
    "sessionId": "uuid",
    "userId": "string",
    "type": "conversation|summary|knowledge",
    "content": "text",
    "timestamp": "datetime",
    "importance": 0.0-1.0,
    "metadata": {}
  }
}
```

## 6. API 规范定义

### 6.1 基础约定
- 基础路径: `/api/v1`
- 格式: JSON (请求/响应均使用 `application/json`)
- 认证: `Authorization: Bearer <token>` 或 `X-API-Key: <key>`
- 分页: `?page=1&size=20` (默认 page=1, size=20)
- 排序: `?sort=createdAt:desc`
- 统一响应格式:
```json
{
  "success": true,
  "data": {},
  "error": null,
  "pagination": { "page": 1, "size": 20, "total": 100 }
}
```

### 6.2 核心 API 端点

#### Agent 管理
```
GET    /api/v1/agents                    # 获取Agent列表
POST   /api/v1/agents                    # 创建Agent
GET    /api/v1/agents/{id}               # 获取Agent详情
PUT    /api/v1/agents/{id}               # 更新Agent
DELETE /api/v1/agents/{id}               # 删除Agent(软删除)
POST   /api/v1/agents/{id}/activate      # 激活Agent
POST   /api/v1/agents/{id}/pause         # 暂停Agent
POST   /api/v1/agents/{id}/archive       # 归档Agent
GET    /api/v1/agents/{id}/versions      # 版本历史
POST   /api/v1/agents/{id}/versions      # 创建新版本
POST   /api/v1/agents/{id}/test          # 测试Agent
GET    /api/v1/agents/{id}/health        # 健康检查
```

#### 聊天/对话
```
POST   /api/v1/chat/completions          # 非流式对话
POST   /api/v1/chat/stream               # 流式对话(SSE)
POST   /api/v1/chat/{sessionId}/message  # 发送消息到指定会话
POST   /api/v1/chat/sessions             # 创建新会话
GET    /api/v1/chat/sessions             # 获取会话列表
GET    /api/v1/chat/sessions/{id}        # 获取会话详情
DELETE /api/v1/chat/sessions/{id}        # 关闭会话
GET    /api/v1/chat/sessions/{id}/messages  # 获取会话消息
```

#### 技能管理
```
GET    /api/v1/skills                    # 获取技能列表
POST   /api/v1/skills                    # 注册技能
GET    /api/v1/skills/{id}               # 获取技能详情
PUT    /api/v1/skills/{id}               # 更新技能
DELETE /api/v1/skills/{id}               # 删除技能
POST   /api/v1/skills/{id}/test          # 测试技能
```

#### 模型管理
```
GET    /api/v1/models/providers          # 获取模型提供商列表
POST   /api/v1/models/providers          # 添加模型提供商
PUT    /api/v1/models/providers/{id}     # 更新提供商
DELETE /api/v1/models/providers/{id}     # 删除提供商
GET    /api/v1/models/providers/{id}/endpoints  # 获取端点列表
POST   /api/v1/models/providers/{id}/endpoints  # 添加端点
GET    /api/v1/models/available          # 获取可用模型列表
GET    /api/v1/models/metrics            # 模型性能指标
```

#### MCP 管理
```
GET    /api/v1/mcp/endpoints             # MCP端点列表
POST   /api/v1/mcp/endpoints             # 注册MCP端点
PUT    /api/v1/mcp/endpoints/{id}        # 更新端点
DELETE /api/v1/mcp/endpoints/{id}        # 删除端点
POST   /api/v1/mcp/endpoints/{id}/sync   # 同步工具
GET    /api/v1/mcp/endpoints/{id}/tools  # 获取工具列表
```

#### 使用量和计费
```
GET    /api/v1/usage/agents/{id}         # Agent使用量
GET    /api/v1/usage/summary             # 使用量汇总
GET    /api/v1/usage/daily               # 日使用量明细
GET    /api/v1/billing/rates             # 费率配置
```

#### 健康检查和监控
```
GET    /api/v1/health                    # 基础健康检查
GET    /api/v1/health/ready              # 就绪检查
GET    /api/v1/health/live               # 存活检查
GET    /api/v1/health/metrics            # 系统指标
```

### 6.3 前端 API 集成层

前端通过统一的 HTTP 客户端 (`src/utils/http.ts`) 对接后端 API，自动处理 Token 刷新、错误拦截、请求重试。同时使用 SignalR 连接实现流式对话和实时通知。

```typescript
// API 响应统一类型
interface ApiResponse<T> {
  success: boolean
  data: T | null
  error: string | null
  pagination?: {
    page: number
    size: number
    total: number
  }
}

// 前端 API 模块划分
// src/api/agent.ts          - Agent 管理 API
// src/api/chat.ts           - 对话 API
// src/api/skill.ts          - 技能管理 API
// src/api/model.ts          - 模型管理 API
// src/api/mcp.ts            - MCP 管理 API
// src/api/session.ts        - 会话管理 API
// src/api/usage.ts          - 使用量统计 API
// src/api/auth.ts           - 认证授权 API
```

## 7. 前端设计

### 7.1 技术架构

前端采用 **Vue 3 + TypeScript + Vite** 技术栈，分为两个独立的前端应用：

| 应用 | 定位 | 说明 |
|------|------|------|
| **agent-platform-admin** | 管理后台 | 面向平台管理员，管理Agent、模型、技能、MCP、用户、计费等 |
| **agent-platform-chat** | 对话界面 | 面向终端用户，提供与Agent交互的对话界面 |

两个应用共享 `ui/shared` 目录下的 TypeScript 类型定义和 API 契约，确保前后端类型一致。

### 7.2 路由设计

#### 管理后台路由 (`agent-platform-admin`)

```
/login                              # 登录页
/                                   # 首页/仪表盘(Dashboard)
/agents                             # Agent 列表
/agents/create                      # 创建 Agent
/agents/:id                         # Agent 详情
/agents/:id/edit                    # 编辑 Agent
/agents/:id/versions                # 版本历史
/agents/:id/test                    # 在线测试 Agent
/models                             # 模型提供商列表
/models/providers/:id               # 提供商详情(含端点管理)
/models/providers/create            # 添加提供商
/skills                             # 技能列表
/skills/create                      # 注册技能
/skills/:id                         # 技能详情
/skills/:id/edit                    # 编辑技能
/mcp/endpoints                      # MCP 端点列表
/mcp/endpoints/create               # 注册 MCP 端点
/mcp/endpoints/:id                  # MCP 端点详情
/sessions                           # 会话管理
/sessions/:id                       # 会话详情(消息追溯)
/usage                              # 使用量统计
/usage/daily                        # 日使用量明细
/usage/agents/:id                   # Agent 详细用量
/billing/rates                      # 费率配置
/users                              # 用户管理
/api-keys                           # API Key 管理
/audit-logs                         # 审计日志
/settings                           # 系统设置
```

#### 对话界面路由 (`agent-platform-chat`)

```
/chat                               # 对话主页
/chat/:sessionId                    # 指定会话对话
/chat/agents                        # 可选 Agent 列表
/chat/history                       # 历史会话列表
```

### 7.3 页面与组件设计

#### 7.3.1 管理后台页面清单

| 页面 | 路由 | 关键组件 | 功能说明 |
|------|------|---------|---------|
| **仪表盘** | `/` | `DashboardMetrics`, `UsageChart`, `AgentStatusCard`, `RecentActivityList` | 系统概览，Agent状态分布、使用量趋势、健康状态 |
| **Agent列表** | `/agents` | `AgentTable`, `AgentStatusBadge`, `AgentFilterBar`, `BatchActionBar` | 搜索/筛选/排序，批量操作，状态切换 |
| **Agent创建/编辑** | `/agents/create`, `/agents/:id/edit` | `AgentBasicForm`, `PromptEditor`, `ModelConfigForm`, `SkillBindingPanel`, `McpBindingPanel`, `ParameterSlider` | 分步表单，系统提示词编辑器(支持Markdown预览)，模型参数滑条，技能绑定，MCP绑定 |
| **Agent详情** | `/agents/:id` | `AgentInfoCard`, `StatusTimeline`, `VersionHistory`, `HealthCheckPanel`, `UsageStats` | Agent信息概览，运行状态时间线，版本历史，健康检查结果 |
| **Agent在线测试** | `/agents/:id/test` | `ChatPanel`, `MessageBubble`, `ToolCallDisplay`, `TokenUsageBar`, `DebugPanel` | 内嵌对话面板，实时调试信息，Token消耗追踪 |
| **模型提供商** | `/models` | `ProviderTable`, `ProviderStatusBadge`, `HealthIndicator` | 提供商列表，健康状态，优先级管理 |
| **提供商详情** | `/models/providers/:id` | `ProviderInfoForm`, `EndpointTable`, `EndpointForm`, `MetricsChart` | 编辑提供商信息，管理多个端点，负载均衡配置，性能指标 |
| **技能管理** | `/skills` | `SkillTable`, `SkillTypeTag`, `SkillFilterBar` | 技能列表，按类型筛选 |
| **技能注册** | `/skills/create` | `SkillForm`, `SchemaEditor` | JSON Schema编辑器，端点配置 |
| **MCP端点** | `/mcp/endpoints` | `McpEndpointTable`, `ToolListCard`, `SyncButton` | 端点列表，工具列表展示，手动同步 |
| **MCP端点详情** | `/mcp/endpoints/:id` | `EndpointDetailForm`, `ToolTree` | 编辑端点，查看已发现工具树 |
| **会话管理** | `/sessions` | `SessionTable`, `SessionStatusBadge`, `MessagePreview` | 会话列表，筛选 |
| **会话详情** | `/sessions/:id` | `MessageTimeline`, `TokenBreakdown`, `UsageRecordList` | 完整消息追溯，Token消耗明细 |
| **使用量统计** | `/usage` | `UsageOverviewChart`, `TopAgentsTable`, `DailyTrendChart`, `CostBreakdownPie` | 使用量概览，排名，趋势，费用构成 |
| **API Key管理** | `/api-keys` | `ApiKeyTable`, `CreateKeyDialog`, `KeyDisplayDialog` | Key列表，创建新Key(仅显示一次)，启用/禁用 |
| **审计日志** | `/audit-logs` | `AuditLogTable`, `AuditFilterBar`, `AuditDetailDrawer` | 操作日志查询，筛选，详情查看 |
| **用户管理** | `/users` | `UserTable`, `UserForm`, `RoleAssignPanel` | 用户CRUD，角色分配，权限管理 |

#### 7.3.2 对话界面页面清单

| 页面 | 路由 | 关键组件 | 功能说明 |
|------|------|---------|---------|
| **对话主页** | `/chat` | `MessageList`, `ChatInput`, `AgentSelector`, `SessionSidebar`, `FileUploader`, `MarkdownRenderer` | 完整的对话界面，消息列表，输入区，Agent切换，会话侧边栏 |
| **对话详情** | `/chat/:sessionId` | `MessageList`, `ChatInput`, `LoadingBubble`, `ToolCallBubble` | 指定会话的对话，保留历史上下文 |
| **历史会话** | `/chat/history` | `HistoryList`, `SearchBar`, `SessionCard` | 历史会话搜索和浏览 |

### 7.4 核心组件设计

#### 7.4.1 公共组件 (`src/components/`)

```
common/
├── AppTable.vue              # 通用表格(封装ElTable，支持排序/筛选/分页/列自定义)
├── AppForm.vue               # 通用表单(封装ElForm，支持动态表单项)
├── AppDialog.vue             # 通用对话框
├── AppDrawer.vue             # 通用抽屉
├── AppCard.vue               # 通用卡片
├── StatusBadge.vue           # 状态标签组件
├── JsonViewer.vue            # JSON 查看器(格式化和高亮)
├── JsonEditor.vue            # JSON 编辑器(基于CodeMirror/Monaco)
├── MarkdownRenderer.vue      # Markdown 渲染器
├── ConfirmDialog.vue         # 确认对话框
├── EmptyState.vue            # 空状态占位
├── LoadingSkeleton.vue       # 骨架屏
├── Pagination.vue            # 分页组件
├── SearchBar.vue             # 搜索栏组件
├── FileUploader.vue          # 文件上传组件
├── ColorPicker.vue           # 颜色选择器
└── IconPicker.vue            # 图标选择器
```

#### 7.4.2 Agent 专用组件

```
agent/
├── AgentBasicForm.vue        # Agent 基本信息表单
├── AgentTable.vue            # Agent 列表表格
├── AgentCard.vue             # Agent 信息卡片
├── AgentStatusBadge.vue      # Agent 状态标签
├── AgentStatusTimeline.vue   # 状态变更时间线
├── AgentFilterBar.vue        # Agent 筛选栏
├── VersionHistory.vue        # 版本历史列表
├── VersionDiff.vue           # 版本差异对比
├── PromptEditor.vue          # 系统提示词编辑器(支持变量插入、Markdown预览)
└── HealthCheckPanel.vue      # 健康检查结果面板
```

#### 7.4.3 模型配置组件

```
model/
├── ModelConfigForm.vue       # 模型参数配置表单(Temperature/MaxTokens/TopP等滑条)
├── ProviderTable.vue         # 模型提供商表格
├── ProviderForm.vue          # 提供商表单
├── EndpointTable.vue         # 端点列表
├── EndpointForm.vue          # 端点编辑表单
├── ModelMetricsChart.vue     # 模型性能指标图表
├── ModelSelector.vue         # 模型选择器(从可用模型列表中选取)
└── RouteStrategyConfig.vue   # 负载均衡策略配置
```

#### 7.4.4 技能/MCP 组件

```
skill/
├── SkillTable.vue            # 技能列表
├── SkillForm.vue             # 技能注册表单
├── SkillBindingPanel.vue     # Agent绑定的多选技能面板
├── SchemaEditor.vue          # JSON Schema 编辑器
├── SkillTestPanel.vue        # 技能在线测试面板
└── SkillTypeTag.vue          # 技能类型标签

mcp/
├── McpEndpointTable.vue      # MCP 端点列表
├── McpEndpointForm.vue       # MCP 端点表单
├── McpBindingPanel.vue       # Agent绑定MCP端点面板
├── ToolListCard.vue          # 工具列表卡片
├── ToolTree.vue              # 工具树形展示
└── McpSyncButton.vue         # 同步按钮(带状态)
```

#### 7.4.5 对话相关组件

```
chat/
├── MessageList.vue           # 消息列表(虚拟滚动)
├── MessageBubble.vue         # 单条消息气泡
├── ChatInput.vue             # 聊天输入区(支持多行、Markdown快捷输入、文件上传)
├── ChatPanel.vue             # 对话面板(整合消息列表+输入区)
├── SessionSidebar.vue        # 会话侧边栏
├── SessionCard.vue           # 会话卡片
├── AgentSelector.vue         # Agent选择器(下拉/卡片)
├── TokenUsageBar.vue         # Token 消耗进度条
├── ToolCallBubble.vue        # 工具调用气泡(含调用参数和返回结果)
├── DebugPanel.vue            # 调试面板(显示完整请求/响应日志)
├── FileUploader.vue          # 文件上传(拖拽+点击)
└── MarkdownRenderer.vue      # Markdown渲染(代码高亮、表格、LaTeX)
```

#### 7.4.6 统计图表组件

```
charts/
├── UsageTrendChart.vue       # 使用量趋势折线图
├── AgentDistributionChart.vue # Agent状态分布饼图
├── CostBreakdownChart.vue    # 费用构成柱状图
├── DailyActiveChart.vue      # 日活跃度热力图
├── TopAgentsChart.vue        # Top Agent排名条形图
├── ModelPerformanceChart.vue # 模型性能雷达图
└── TokenUsageOverTime.vue    # Token消耗时间序列图
```

### 7.5 状态管理设计 (Pinia Store)

```typescript
// stores/agent.ts         - Agent 列表、当前选中Agent、Agent筛选条件
// stores/chat.ts          - 当前会话消息、会话列表、流式接收状态
// stores/model.ts         - 模型提供商列表、可用模型、当前选择
// stores/skill.ts         - 技能列表、技能筛选
// stores/mcp.ts           - MCP端点列表、工具缓存
// stores/session.ts       - 会话列表、当前会话
// stores/usage.ts         - 使用量数据、统计周期
// stores/auth.ts          - 用户信息、Token、权限、登录状态
// stores/app.ts           - 全局状态(主题、语言、侧边栏折叠、布局)
// stores/notification.ts  - 通知消息(实时告警、操作反馈)
```

每个 Store 遵循以下模式：
```typescript
// 示例: stores/agent.ts
import { defineStore } from 'pinia'

export const useAgentStore = defineStore('agent', () => {
  // State
  const agents = ref<Agent[]>([])
  const currentAgent = ref<AgentDetail | null>(null)
  const loading = ref(false)
  const filters = reactive<AgentFilters>({ status: '', search: '', page: 1, size: 20 })

  // Actions
  async function fetchAgents() { /* 调用API获取列表 */ }
  async function fetchAgentDetail(id: string) { /* 获取详情 */ }
  async function createAgent(data: CreateAgentDto) { /* 创建 */ }
  async function updateAgent(id: string, data: UpdateAgentDto) { /* 更新 */ }
  async function deleteAgent(id: string) { /* 删除 */ }
  async function activateAgent(id: string) { /* 激活 */ }
  async function pauseAgent(id: string) { /* 暂停 */ }

  // Getters
  const activeAgents = computed(() => agents.value.filter(a => a.status === 'Active'))
  const agentCountByStatus = computed(() => { /* 按状态统计 */ })

  return {
    agents, currentAgent, loading, filters,
    fetchAgents, fetchAgentDetail, createAgent, updateAgent, deleteAgent,
    activateAgent, pauseAgent,
    activeAgents, agentCountByStatus
  }
})
```

### 7.6 HTTP 客户端设计

```typescript
// src/utils/http.ts
import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios'
import { useAuthStore } from '@/stores/auth'
import { ElMessage } from 'element-plus'
import router from '@/router'

const http: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' }
})

// 请求拦截器 - 自动注入Token
http.interceptors.request.use((config) => {
  const authStore = useAuthStore()
  if (authStore.token) {
    config.headers.Authorization = `Bearer ${authStore.token}`
  }
  // API Key 认证也支持
  if (authStore.apiKey) {
    config.headers['X-API-Key'] = authStore.apiKey
  }
  return config
})

// 响应拦截器 - 统一错误处理 + Token自动刷新
http.interceptors.response.use(
  (response: AxiosResponse<ApiResponse<any>>) => {
    return response
  },
  async (error) => {
    if (error.response?.status === 401) {
      // Token 过期，尝试刷新
      const authStore = useAuthStore()
      const refreshed = await authStore.refreshToken()
      if (refreshed) {
        return http(error.config)
      }
      // 刷新失败，跳转登录
      authStore.logout()
      router.push('/login')
    }
    ElMessage.error(error.response?.data?.error || '请求失败')
    return Promise.reject(error)
  }
)

export default http
```

### 7.7 SignalR 实时通信

流式对话和实时通知通过 SignalR 实现：

```typescript
// src/utils/signalr.ts
import * as signalR from '@microsoft/signalr'

class SignalRService {
  private connection: signalR.HubConnection | null = null

  async connect(token: string) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_API_BASE_URL}/hubs/chat`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build()

    // 注册流式消息处理
    this.connection.on('MessageReceived', (message: ChatMessage) => {
      // 更新消息列表
    })

    this.connection.on('TokenStream', (chunk: string) => {
      // 处理流式Token
    })

    this.connection.on('ToolCallExecuted', (toolCall: ToolCallResult) => {
      // 处理工具调用结果
    })

    this.connection.on('AgentStatusChanged', (status: AgentStatus) => {
      // Agent状态变更通知
    })

    await this.connection.start()
  }

  async sendMessage(sessionId: string, content: string) {
    await this.connection?.invoke('SendMessage', sessionId, content)
  }

  async disconnect() {
    await this.connection?.stop()
  }
}

export const signalRService = new SignalRService()
```

### 7.8 样式与主题

- 基于 Element Plus 的 CSS Variables 实现主题切换（亮色/暗色）
- 使用 SCSS 管理全局变量和混入
- 布局采用左侧固定导航 + 右侧内容区的经典管理后台布局
- 对话界面采用全屏对话式布局
- 响应式适配：管理后台最小支持 1280px 宽度，对话界面适配桌面和平板

### 7.9 国际化 (i18n)

```typescript
// src/locales/
//   zh-CN.ts             - 中文语言包
//   en.ts                - 英文语言包

export default {
  // 按模块组织
  common: {
    save: '保存',
    cancel: '取消',
    confirm: '确认',
    delete: '删除',
    edit: '编辑',
    create: '创建',
    search: '搜索',
    reset: '重置',
    export: '导出',
    status: { active: '活跃', paused: '已暂停', archived: '已归档', draft: '草稿' }
  },
  agent: {
    title: 'Agent 管理',
    name: '名称',
    description: '描述',
    systemPrompt: '系统提示词',
    status: '状态',
    model: '关联模型',
    skills: '绑定技能',
    mcp: 'MCP端点'
  },
  model: { /* ... */ },
  skill: { /* ... */ },
  mcp: { /* ... */ },
  session: { /* ... */ },
  usage: { /* ... */ },
  auth: {
    login: '登录',
    logout: '退出登录',
    username: '用户名',
    password: '密码',
    apiKey: 'API Key'
  }
}
```

### 7.10 关键交互流程

#### 7.10.1 Agent 创建流程
```
用户点击 "创建Agent"
  -> 步骤1: 基本信息(名称、描述、头像) [AgentBasicForm]
  -> 步骤2: 系统提示词 [PromptEditor] (支持变量 {{username}}, {{date}} 等)
  -> 步骤3: 模型配置 [ModelConfigForm] (选择提供商、模型ID、参数调优)
  -> 步骤4: 技能绑定 [SkillBindingPanel] (多选、排序)
  -> 步骤5: MCP 绑定 [McpBindingPanel] (选择MCP端点)
  -> 预览确认 -> 提交创建
  -> 成功后跳转 Agent 详情页
```

#### 7.10.2 对话交互流程
```
用户选择 Agent -> 打开对话界面
  -> 创建/恢复会话
  -> 输入消息 -> 发送(SSE/SignalR)
  -> 实时显示 Token 流
  -> 如果触发工具调用: 显示 ToolCallBubble(含参数、结果)
  -> 完整响应展示(Markdown渲染、代码高亮)
  -> Token 消耗显示
  -> 继续对话或结束会话
```

#### 7.10.3 长期记忆展示流程
```
用户进入 Agent 详情页
  -> 查看 "记忆" 选项卡
  -> 展示记忆列表(时间线 + 摘要)
  -> 搜索/过滤记忆内容
  -> 查看记忆详情
  -> 可手动删除/标记记忆
```

## 8. 关键流程设计

### 8.1 Agent 执行流程
```
用户请求 -> 认证授权 -> 速率限制检查 -> 会话查找/创建
  -> 上下文组装(系统提示 + 短期记忆 + 长期记忆检索)
  -> 模型调用(选择提供商 -> 负载均衡 -> API调用)
  -> 响应解析 -> 技能调用(若触发工具调用)
  -> 记忆更新(短期记忆存入Redis, 异步整合到长期记忆)
  -> 使用量记录 -> 返回响应
```

### 8.2 长期记忆整合流程 (Background Service)
```
每 N 轮对话 -> 生成摘要向量
  -> 计算重要性分数
  -> 存入向量数据库
  -> 定期清理低重要性记忆
  -> 合并相似记忆(去重)
```

## 9. 实施路线图

### Phase 1: 核心框架 + 前端基础 (2-3周)
**后端:**
- 项目结构和模块化架构搭建
- Core 领域模型和核心接口定义
- EF Core 数据访问层 + 数据库迁移
- ASP.NET Core API 基础框架 + Swagger
- JWT 认证 + API Key 认证

**前端:**
- Vite + Vue 3 + TypeScript 项目初始化
- 布局组件 (LayoutBase、Sidebar、Header)
- 路由框架搭建和路由守卫
- Pinia Store 框架 (auth, app)
- HTTP 客户端和拦截器配置
- 登录页面
- 通用组件库搭建 (AppTable, AppForm, AppDialog)

### Phase 2: Agent 引擎 + 管理后台核心 (2-3周)
**后端:**
- Agent 生命周期管理 (CRUD + 状态机)
- OpenAI 模型提供程序集成
- 基础聊天对话 API
- 会话管理

**前端 (管理后台):**
- Agent 列表 + 创建/编辑 + 详情页
- Agent 状态管理和图表展示
- 模型提供商管理页面
- 对话测试页面 (集成ChatPanel)

### Phase 3: 核心组件 + 完整管理后台 (2-3周)
**后端:**
- 技能系统 (接口 + 注册 + 执行)
- MCP 集成
- 短期记忆 (Redis)
- 流式响应 (SSE + SignalR)

**前端 (管理后台):**
- 技能管理页面 (列表 + 注册 + Schema编辑器)
- MCP 端点管理页面 (端点 + 工具树)
- 会话管理页面 (列表 + 消息追溯)
- 统一的 SignalR 实时通信集成

### Phase 4: 高级功能 + 对话界面 (2-3周)
**后端:**
- 长期记忆 (向量数据库)
- 模型负载均衡和路由
- 使用量跟踪和计费
- 审计日志

**前端 (对话界面 + 管理后台):**
- 完整对话界面 (MessageList + ChatInput + 流式展示)
- 会话侧边栏和管理
- 使用量统计仪表盘 (ECharts图表)
- 审计日志页面
- API Key 管理页面
- 用户管理页面

### Phase 5: 平台完善 + 端到端集成 (1-2周)
**全栈:**
- 速率限制
- 健康检查和监控
- 性能优化
- 国际化 (zh-CN + en)
- 主题切换 (亮色/暗色)
- 容器化部署配置 (Docker Compose + Nginx)
- 集成测试 + E2E 测试
- 前后端联调验收

## 10. 关键设计约束

1. **模块化单体架构**: 各模块通过接口依赖，不直接耦合，未来可拆分微服务
2. **OpenAI API 兼容**: 模型提供程序抽象为 `IModelProvider`，方便扩展其他提供商
3. **混合存储**: SQL 存结构化数据，Redis 存缓存/短期记忆，向量库存语义记忆
4. **灵活部署**: Docker Compose 配置 + Windows Service 配置并行支持
5. **安全优先**: API Key 加密存储、敏感字段审计、HTTPS 强制、速率限制

## 11. 交付物清单

- 完整源码 (包括前端和后端)
- 数据库迁移脚本 (EF Core Migrations)
- Nginx 反向代理配置
- Docker Compose 部署文件 (后端 + 前端 + 数据库 + Redis + Qdrant)
- OpenAPI 接口文档 (自动生成)
- 单元测试、集成测试和 E2E 测试
- 配置文件模板 (appsettings.json + .env)
- Element Plus 主题配置