# BPM 工作流引擎 — 详细实现设计

> 版本：1.0 | 日期：2026-03-13 | 基于：workflow-engine-design.md v1.0

---

## 目录

1. [技术栈与依赖](#一技术栈与依赖)
2. [项目结构](#二项目结构)
3. [数据库设计](#三数据库设计)
4. [数据库抽象层](#四数据库抽象层)
5. [API 接口规范](#五api-接口规范)
6. [流程定义模块](#六流程定义模块)
7. [提交流程实现](#七提交流程实现)
8. [审批操作实现](#八审批操作实现)
9. [动态审批人管理](#九动态审批人管理)
10. [代理与委托模块](#十代理与委托模块)
11. [任务列表模块](#十一任务列表模块)
12. [扩展点集成实现](#十二扩展点集成实现)
13. [通知服务实现](#十三通知服务实现)
14. [并发控制](#十四并发控制)
15. [测试策略](#十五测试策略)

---

## 一、技术栈与依赖

### 1.1 运行时与框架

| 组件 | 选型 | 版本 | 用途 |
|------|------|------|------|
| 运行时 | .NET | 8.0 LTS | 主运行时 |
| Web 框架 | ASP.NET Core | 8.0 | REST API |
| ORM | Entity Framework Core | 8.0 | 数据访问 |
| DB 驱动 | Npgsql.EntityFrameworkCore.PostgreSQL | 8.0 | PostgreSQL 适配 |
| In-Memory DB | EF Core In-Memory | 8.0 | 单元测试 mock |
| 缓存/分布式锁 | StackExchange.Redis | 2.7 | Redis 客户端 |
| 消息队列 | MassTransit + RabbitMQ.Client | 8.x | 异步通知解耦 |
| CQRS/中介者 | MediatR | 12.x | 命令/查询/事件分发 |
| 验证 | FluentValidation.AspNetCore | 11.x | 请求参数校验 |
| HTTP 弹性 | Polly | 8.x | 扩展点 HTTP 回调重试 |
| HTTP 客户端 | Microsoft.Extensions.Http.Polly | 8.0 | 注入弹性 HttpClient |
| 日志 | Serilog.AspNetCore | 8.x | 结构化日志 |
| API 文档 | Swashbuckle.AspNetCore | 6.x | Swagger UI |
| 对象映射 | Mapster | 7.x | DTO 映射（轻量，性能优于 AutoMapper） |

### 1.2 NuGet 包清单（核心）

```xml
<!-- WorkflowEngine.Infrastructure.csproj -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.*" />
<PackageReference Include="StackExchange.Redis" Version="2.7.*" />
<PackageReference Include="MassTransit.RabbitMQ" Version="8.2.*" />
<PackageReference Include="Polly.Extensions.Http" Version="3.0.*" />

<!-- WorkflowEngine.Application.csproj -->
<PackageReference Include="MediatR" Version="12.2.*" />
<PackageReference Include="FluentValidation" Version="11.9.*" />
<PackageReference Include="Mapster" Version="7.4.*" />

<!-- WorkflowEngine.API.csproj -->
<PackageReference Include="Serilog.AspNetCore" Version="8.0.*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.*" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.*" />
```

---

## 二、项目结构

采用**Clean Architecture（整洁架构）**，依赖方向：API → Application → Domain，Infrastructure 实现 Domain/Application 定义的接口。

```
WorkflowEngine/
├── WorkflowEngine.Domain/                  # 领域层（无外部依赖）
│   ├── Entities/
│   │   ├── ProcessDefinition.cs
│   │   ├── ProcessInstance.cs
│   │   ├── ApprovalStep.cs
│   │   ├── WorkflowTask.cs
│   │   ├── ApprovalRule.cs
│   │   ├── ApproverListModification.cs
│   │   ├── ProxyConfig.cs
│   │   └── DelegationConfig.cs
│   ├── Enums/
│   │   ├── ProcessStatus.cs
│   │   ├── StepType.cs
│   │   ├── TaskAction.cs
│   │   └── JointSignPolicy.cs
│   ├── Events/                             # 领域事件（供 MediatR 处理）
│   │   ├── ProcessSubmittedEvent.cs
│   │   ├── TaskCompletedEvent.cs
│   │   ├── ProcessCompletedEvent.cs
│   │   └── ProcessRejectedEvent.cs
│   ├── Repositories/                       # 仓储接口（Domain 定义，Infrastructure 实现）
│   │   ├── IRepository.cs
│   │   ├── IUnitOfWork.cs
│   │   ├── IProcessDefinitionRepository.cs
│   │   ├── IProcessInstanceRepository.cs
│   │   ├── IApprovalStepRepository.cs
│   │   ├── ITaskRepository.cs
│   │   ├── IApprovalRuleRepository.cs
│   │   ├── IProxyConfigRepository.cs
│   │   └── IDelegationConfigRepository.cs
│   └── Exceptions/
│       ├── WorkflowException.cs
│       ├── PermissionDeniedException.cs
│       └── InvalidOperationException.cs
│
├── WorkflowEngine.Application/             # 应用层（用例编排）
│   ├── Commands/                           # 写操作命令（MediatR IRequest）
│   │   ├── Submission/
│   │   │   ├── PrepareSubmitCommand.cs
│   │   │   └── SubmitProcessCommand.cs
│   │   ├── Approval/
│   │   │   ├── ApproveTaskCommand.cs
│   │   │   ├── RejectTaskCommand.cs
│   │   │   ├── ReturnTaskCommand.cs
│   │   │   └── CountersignTaskCommand.cs
│   │   ├── DynamicStep/
│   │   │   ├── InsertStepCommand.cs
│   │   │   ├── DeleteStepCommand.cs
│   │   │   └── ReplaceAssigneesCommand.cs
│   │   ├── Proxy/
│   │   │   ├── CreateProxyConfigCommand.cs
│   │   │   └── DeleteProxyConfigCommand.cs
│   │   └── Delegation/
│   │       ├── CreateDelegationCommand.cs
│   │       └── UpdateDelegationCommand.cs
│   ├── Queries/                            # 读操作查询
│   │   ├── Tasks/
│   │   │   ├── GetPendingTasksQuery.cs
│   │   │   └── GetCompletedTasksQuery.cs
│   │   ├── Instances/
│   │   │   ├── GetInstanceDetailQuery.cs
│   │   │   └── GetInstanceHistoryQuery.cs
│   │   └── Proxy/
│   │       └── GetMyPrincipalsQuery.cs
│   ├── Services/                           # 扩展点接口（Application 定义）
│   │   ├── IApproverResolver.cs
│   │   ├── IPermissionValidator.cs
│   │   ├── IDistributedLockService.cs
│   │   └── INotificationPublisher.cs
│   ├── DTOs/                               # 数据传输对象
│   │   ├── TaskDto.cs
│   │   ├── ProcessInstanceDto.cs
│   │   └── ApprovalStepDto.cs
│   └── Validators/                         # FluentValidation 验证器
│       ├── SubmitProcessValidator.cs
│       └── ApproveTaskValidator.cs
│
├── WorkflowEngine.Infrastructure/          # 基础设施层
│   ├── Persistence/
│   │   ├── WorkflowDbContext.cs
│   │   ├── Configurations/                 # EF Core Fluent API 配置
│   │   │   ├── ProcessDefinitionConfig.cs
│   │   │   ├── ProcessInstanceConfig.cs
│   │   │   └── ...
│   │   ├── Repositories/                   # 仓储实现
│   │   │   ├── ProcessInstanceRepository.cs
│   │   │   ├── TaskRepository.cs
│   │   │   └── ...
│   │   └── UnitOfWork.cs
│   ├── ExternalServices/
│   │   ├── HttpApproverResolver.cs         # HTTP 回调实现
│   │   └── HttpPermissionValidator.cs
│   ├── Messaging/
│   │   ├── RabbitMqNotificationPublisher.cs
│   │   └── Consumers/
│   │       └── NotificationConsumer.cs
│   ├── Caching/
│   │   └── RedisDistributedLockService.cs
│   └── DependencyInjection.cs             # Infrastructure DI 注册
│
├── WorkflowEngine.API/                     # API 层
│   ├── Controllers/
│   │   ├── ProcessDefinitionsController.cs
│   │   ├── ProcessInstancesController.cs
│   │   ├── TasksController.cs
│   │   ├── ApprovalRulesController.cs
│   │   ├── ProxyConfigsController.cs
│   │   └── DelegationConfigsController.cs
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs
│   │   └── CurrentUserMiddleware.cs
│   ├── Models/                             # API 请求/响应模型
│   │   ├── Requests/
│   │   └── Responses/
│   └── Program.cs
│
└── WorkflowEngine.Tests/
    ├── Unit/                               # 单元测试（In-Memory DB）
    │   ├── Application/
    │   └── Domain/
    └── Integration/                        # 集成测试（TestContainers PostgreSQL）
        └── API/
```

---

## 三、数据库设计

### 3.1 完整 DDL

```sql
-- 启用 UUID 生成
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================
-- 流程定义
-- ============================================================
CREATE TABLE process_definitions (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                    VARCHAR(200) NOT NULL,
    process_type            VARCHAR(100) NOT NULL,           -- 业务类型标识，如 expense_report
    version                 INT         NOT NULL DEFAULT 1,
    status                  VARCHAR(20)  NOT NULL DEFAULT 'draft', -- draft | active | archived
    node_templates          JSONB        NOT NULL DEFAULT '[]',    -- 默认节点模板
    rule_set_id             UUID,
    approver_resolver_url   VARCHAR(500),
    permission_validator_url VARCHAR(500),
    created_by              VARCHAR(100) NOT NULL,
    created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_process_type_version UNIQUE (process_type, version)
);

CREATE INDEX idx_pd_process_type   ON process_definitions (process_type);
CREATE INDEX idx_pd_status         ON process_definitions (status);

-- ============================================================
-- 流程实例
-- ============================================================
CREATE TABLE process_instances (
    id                   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    definition_id        UUID        NOT NULL REFERENCES process_definitions(id),
    definition_version   INT         NOT NULL,
    business_key         VARCHAR(200) NOT NULL,
    form_data_snapshot   JSONB        NOT NULL DEFAULT '{}',
    submitted_by         VARCHAR(100) NOT NULL,              -- 实际操作人
    on_behalf_of         VARCHAR(100),                       -- 被代理人（可空）
    status               VARCHAR(30)  NOT NULL DEFAULT 'running', -- running | completed | rejected | withdrawn
    is_urgent            BOOLEAN      NOT NULL DEFAULT FALSE,
    current_step_index   DECIMAL(10,4),
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    completed_at         TIMESTAMPTZ,
    CONSTRAINT uq_business_key UNIQUE (business_key)
);

CREATE INDEX idx_pi_status         ON process_instances (status);
CREATE INDEX idx_pi_submitted_by   ON process_instances (submitted_by);
CREATE INDEX idx_pi_on_behalf_of   ON process_instances (on_behalf_of);
CREATE INDEX idx_pi_created_at     ON process_instances (created_at DESC);

-- ============================================================
-- 审批步骤
-- ============================================================
CREATE TABLE approval_steps (
    id                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    instance_id       UUID        NOT NULL REFERENCES process_instances(id) ON DELETE CASCADE,
    step_index        DECIMAL(10,4) NOT NULL,               -- 浮点数，支持插入中间步骤（如 1.5）
    type              VARCHAR(30)  NOT NULL,                 -- approval | joint_sign | notify
    assignees         JSONB        NOT NULL DEFAULT '[]',   -- [{"userId":"uid_001"}]
    joint_sign_policy VARCHAR(20),                           -- ALL_PASS | MAJORITY | ANY_ONE
    status            VARCHAR(30)  NOT NULL DEFAULT 'pending', -- pending | active | completed | rejected | returned | skipped
    source            VARCHAR(30)  NOT NULL DEFAULT 'original', -- original | countersign | dynamic_added
    added_by_user_id  VARCHAR(100),
    added_at          TIMESTAMPTZ,
    completed_at      TIMESTAMPTZ,
    CONSTRAINT uq_step_index UNIQUE (instance_id, step_index)
);

CREATE INDEX idx_as_instance_id    ON approval_steps (instance_id);
CREATE INDEX idx_as_status         ON approval_steps (instance_id, status);
CREATE INDEX idx_as_step_index     ON approval_steps (instance_id, step_index);

-- ============================================================
-- 任务
-- ============================================================
CREATE TABLE tasks (
    id                    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    instance_id           UUID        NOT NULL REFERENCES process_instances(id) ON DELETE CASCADE,
    step_id               UUID        NOT NULL REFERENCES approval_steps(id) ON DELETE CASCADE,
    assignee_id           VARCHAR(100) NOT NULL,             -- 实际处理人（委托后为受托人）
    original_assignee_id  VARCHAR(100),                      -- 原始审批人（委托时保留）
    is_delegated          BOOLEAN      NOT NULL DEFAULT FALSE,
    status                VARCHAR(30)  NOT NULL DEFAULT 'pending', -- pending | completed | returned | rejected | skipped
    is_urgent             BOOLEAN      NOT NULL DEFAULT FALSE,
    action                VARCHAR(30),                        -- approve | reject | return | countersign | notify_read
    comment               TEXT,
    row_version           INT          NOT NULL DEFAULT 0,   -- 乐观锁版本号
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    completed_at          TIMESTAMPTZ
);

-- 待办任务核心查询索引
CREATE INDEX idx_task_assignee_pending
    ON tasks (assignee_id, status)
    WHERE status = 'pending';

-- 已办任务查询索引
CREATE INDEX idx_task_assignee_completed
    ON tasks (assignee_id, completed_at DESC)
    WHERE status IN ('completed', 'returned', 'rejected');

-- 委托任务查询（originalAssigneeId 也可能是当前用户）
CREATE INDEX idx_task_original_assignee
    ON tasks (original_assignee_id)
    WHERE is_delegated = TRUE;

CREATE INDEX idx_task_instance_id       ON tasks (instance_id);
CREATE INDEX idx_task_step_id           ON tasks (step_id);

-- ============================================================
-- 审批规则
-- ============================================================
CREATE TABLE approval_rules (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                    VARCHAR(200) NOT NULL,
    priority                INT         NOT NULL DEFAULT 0,   -- 越大越优先
    process_definition_id   UUID        REFERENCES process_definitions(id),  -- NULL 表示全局规则
    conditions              JSONB        NOT NULL DEFAULT '[]',
    condition_logic         VARCHAR(10)  NOT NULL DEFAULT 'AND', -- AND | OR
    result                  JSONB        NOT NULL DEFAULT '{}',
    is_active               BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_ar_process_def    ON approval_rules (process_definition_id, is_active, priority DESC);
CREATE INDEX idx_ar_global         ON approval_rules (is_active, priority DESC) WHERE process_definition_id IS NULL;

-- ============================================================
-- 审批列表修改记录
-- ============================================================
CREATE TABLE approver_list_modifications (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    instance_id     UUID        NOT NULL REFERENCES process_instances(id) ON DELETE CASCADE,
    modified_by     VARCHAR(100) NOT NULL,
    modified_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    original_steps  JSONB        NOT NULL DEFAULT '[]',       -- 规则引擎返回的原始快照
    final_steps     JSONB        NOT NULL DEFAULT '[]',       -- 用户最终确认的列表
    diff_summary    JSONB        NOT NULL DEFAULT '[]'        -- 结构化 diff
);

CREATE INDEX idx_alm_instance_id   ON approver_list_modifications (instance_id, modified_at DESC);

-- ============================================================
-- 代理配置
-- ============================================================
CREATE TABLE proxy_configs (
    id                    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    principal_id          VARCHAR(100) NOT NULL,              -- 被代理人 B
    agent_id              VARCHAR(100) NOT NULL,              -- 代理人 A
    allowed_process_types JSONB        NOT NULL DEFAULT '[]', -- 空数组=全部流程
    valid_from            TIMESTAMPTZ  NOT NULL,
    valid_to              TIMESTAMPTZ  NOT NULL,
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_pc_agent_id       ON proxy_configs (agent_id, is_active);
CREATE INDEX idx_pc_principal_id   ON proxy_configs (principal_id, is_active);
-- 有效代理配置查询
CREATE INDEX idx_pc_active_range
    ON proxy_configs (agent_id, valid_from, valid_to)
    WHERE is_active = TRUE;

-- ============================================================
-- 委托配置
-- ============================================================
CREATE TABLE delegation_configs (
    id                    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    delegator_id          VARCHAR(100) NOT NULL,              -- 委托人（休假者）
    delegatee_id          VARCHAR(100) NOT NULL,              -- 受托人
    allowed_process_types JSONB        NOT NULL DEFAULT '[]', -- 空数组=全部流程
    valid_from            TIMESTAMPTZ  NOT NULL,
    valid_to              TIMESTAMPTZ  NOT NULL,
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    reason                VARCHAR(500),
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_dc_delegator_id   ON delegation_configs (delegator_id, is_active);
-- 提交时批量检查委托的索引
CREATE INDEX idx_dc_active_range
    ON delegation_configs (delegator_id, valid_from, valid_to)
    WHERE is_active = TRUE;
```

### 3.2 JSONB 字段规范

| 表 | 字段 | JSON Schema 说明 |
|---|---|---|
| process_definitions | node_templates | `[{ "type": "approval\|joint_sign\|notify", "assignees": [...], "policy": "..." }]` |
| process_instances | form_data_snapshot | 业务自定义，引擎透传不解析 |
| approval_steps | assignees | `[{ "userId": "uid_001" }]`（已解析为具体用户ID） |
| approval_rules | conditions | `[{ "field": "amount", "operator": "gt", "value": 10000 }]` |
| approval_rules | result | `{ "steps": [...] }` |
| approver_list_modifications | diff_summary | `[{ "action": "added\|removed\|replaced", "stepIndex": 1.5, "assigneeId": "..." }]` |

---

## 四、数据库抽象层

### 4.1 仓储接口（Domain 层）

```csharp
// Domain/Repositories/IRepository.cs
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(T entity);
    void Update(T entity);
    void Remove(T entity);
}

// Domain/Repositories/IUnitOfWork.cs
public interface IUnitOfWork : IAsyncDisposable
{
    IProcessDefinitionRepository ProcessDefinitions { get; }
    IProcessInstanceRepository   ProcessInstances   { get; }
    IApprovalStepRepository      ApprovalSteps      { get; }
    ITaskRepository              Tasks              { get; }
    IApprovalRuleRepository      ApprovalRules      { get; }
    IProxyConfigRepository       ProxyConfigs       { get; }
    IDelegationConfigRepository  DelegationConfigs  { get; }
    IApproverModificationRepository ApproverModifications { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

// Domain/Repositories/IProcessInstanceRepository.cs
public interface IProcessInstanceRepository : IRepository<ProcessInstance>
{
    Task<ProcessInstance?> GetByBusinessKeyAsync(string businessKey, CancellationToken ct = default);
    Task<ProcessInstance?> GetWithStepsAsync(Guid id, CancellationToken ct = default);
    Task<ProcessInstance?> GetWithStepsAndTasksAsync(Guid id, CancellationToken ct = default);
}

// Domain/Repositories/ITaskRepository.cs
public interface ITaskRepository : IRepository<WorkflowTask>
{
    Task<PagedResult<WorkflowTask>> GetPendingTasksAsync(
        string userId, int page, int pageSize, CancellationToken ct = default);

    Task<PagedResult<WorkflowTask>> GetCompletedTasksAsync(
        string userId, int page, int pageSize, CancellationToken ct = default);

    Task<List<WorkflowTask>> GetActiveTasksByStepAsync(Guid stepId, CancellationToken ct = default);
}

// Domain/Repositories/IApprovalRuleRepository.cs
public interface IApprovalRuleRepository : IRepository<ApprovalRule>
{
    Task<List<ApprovalRule>> GetByProcessTypeAsync(
        string processType, bool includeGlobal = true, CancellationToken ct = default);
}
```

### 4.2 EF Core 实现（PostgreSQL）

```csharp
// Infrastructure/Persistence/WorkflowDbContext.cs
public class WorkflowDbContext : DbContext
{
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    public DbSet<ProcessDefinition>         ProcessDefinitions       { get; set; }
    public DbSet<ProcessInstance>           ProcessInstances         { get; set; }
    public DbSet<ApprovalStep>              ApprovalSteps            { get; set; }
    public DbSet<WorkflowTask>              Tasks                    { get; set; }
    public DbSet<ApprovalRule>              ApprovalRules            { get; set; }
    public DbSet<ApproverListModification>  ApproverModifications    { get; set; }
    public DbSet<ProxyConfig>               ProxyConfigs             { get; set; }
    public DbSet<DelegationConfig>          DelegationConfigs        { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowDbContext).Assembly);
    }
}

// Infrastructure/Persistence/Configurations/ProcessInstanceConfig.cs
public class ProcessInstanceConfig : IEntityTypeConfiguration<ProcessInstance>
{
    public void Configure(EntityTypeBuilder<ProcessInstance> builder)
    {
        builder.ToTable("process_instances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BusinessKey).HasColumnName("business_key").HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.BusinessKey).IsUnique();

        // JSONB 字段：使用 PostgreSQL JSON 列类型
        builder.Property(x => x.FormDataSnapshot)
               .HasColumnName("form_data_snapshot")
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonOptions),
                   v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonOptions)!);

        builder.Property(x => x.Status)
               .HasColumnName("status")
               .HasConversion<string>();

        builder.HasMany(x => x.ApprovalSteps)
               .WithOne()
               .HasForeignKey(s => s.InstanceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

// Infrastructure/Persistence/Configurations/ApprovalStepConfig.cs
public class ApprovalStepConfig : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.ToTable("approval_steps");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.InstanceId, x.StepIndex }).IsUnique();

        builder.Property(x => x.Assignees)
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonOptions),
                   v => JsonSerializer.Deserialize<List<StepAssignee>>(v, JsonOptions)!);

        builder.Property(x => x.Type).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.Source).HasConversion<string>();
        builder.Property(x => x.JointSignPolicy).HasConversion<string?>();
    }
}

// Infrastructure/Persistence/UnitOfWork.cs
public class UnitOfWork : IUnitOfWork
{
    private readonly WorkflowDbContext _ctx;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(WorkflowDbContext ctx,
        IProcessInstanceRepository processInstances,
        ITaskRepository tasks,
        IApprovalStepRepository approvalSteps,
        IApprovalRuleRepository approvalRules,
        IProxyConfigRepository proxyConfigs,
        IDelegationConfigRepository delegationConfigs,
        IApproverModificationRepository approverModifications,
        IProcessDefinitionRepository processDefinitions)
    {
        _ctx = ctx;
        ProcessInstances    = processInstances;
        Tasks               = tasks;
        ApprovalSteps       = approvalSteps;
        ApprovalRules       = approvalRules;
        ProxyConfigs        = proxyConfigs;
        DelegationConfigs   = delegationConfigs;
        ApproverModifications = approverModifications;
        ProcessDefinitions  = processDefinitions;
    }

    public IProcessInstanceRepository   ProcessInstances    { get; }
    public ITaskRepository              Tasks               { get; }
    public IApprovalStepRepository      ApprovalSteps       { get; }
    public IApprovalRuleRepository      ApprovalRules       { get; }
    public IProxyConfigRepository       ProxyConfigs        { get; }
    public IDelegationConfigRepository  DelegationConfigs   { get; }
    public IApproverModificationRepository ApproverModifications { get; }
    public IProcessDefinitionRepository ProcessDefinitions  { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _ctx.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _ctx.Database.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _ctx.SaveChangesAsync(ct);
        await _transaction!.CommitAsync(ct);
    }

    public async Task RollbackAsync(CancellationToken ct = default)
        => await _transaction!.RollbackAsync(ct);

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null) await _transaction.DisposeAsync();
        await _ctx.DisposeAsync();
    }
}
```

### 4.3 DI 注册（支持测试切换）

```csharp
// Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowInfrastructure(
        this IServiceCollection services,
        IConfiguration config,
        bool useInMemory = false)
    {
        if (useInMemory)
        {
            // 测试用：In-Memory DB
            services.AddDbContext<WorkflowDbContext>(opt =>
                opt.UseInMemoryDatabase("WorkflowTestDb"));
        }
        else
        {
            // 生产：PostgreSQL
            services.AddDbContext<WorkflowDbContext>(opt =>
                opt.UseNpgsql(config.GetConnectionString("WorkflowDb"),
                    npgsql => npgsql.MigrationsAssembly("WorkflowEngine.Infrastructure")));
        }

        // 注册仓储
        services.AddScoped<IProcessInstanceRepository, ProcessInstanceRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IApprovalStepRepository, ApprovalStepRepository>();
        services.AddScoped<IApprovalRuleRepository, ApprovalRuleRepository>();
        services.AddScoped<IProxyConfigRepository, ProxyConfigRepository>();
        services.AddScoped<IDelegationConfigRepository, DelegationConfigRepository>();
        services.AddScoped<IApproverModificationRepository, ApproverModificationRepository>();
        services.AddScoped<IProcessDefinitionRepository, ProcessDefinitionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // 扩展点实现
        services.AddScoped<IApproverResolver, HttpApproverResolver>();
        services.AddScoped<IPermissionValidator, HttpPermissionValidator>();

        // Redis 分布式锁
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(config.GetConnectionString("Redis")!));
        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

        // MassTransit + RabbitMQ
        services.AddMassTransit(x =>
        {
            x.AddConsumer<NotificationConsumer>();
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(config.GetConnectionString("RabbitMq"));
                cfg.ConfigureEndpoints(ctx);
            });
        });
        services.AddScoped<INotificationPublisher, RabbitMqNotificationPublisher>();

        return services;
    }
}
```

---

## 五、API 接口规范

### 5.1 通用规范

```
Base URL：/api/v1

认证：Bearer Token（JWT）。所有接口需携带 Authorization: Bearer <token>
当前用户：从 JWT Claims 中提取，通过 ICurrentUserService 注入

分页：QueryString ?page=1&pageSize=20（默认 page=1, pageSize=20）

统一响应格式：
{
  "success": true,
  "data": { ... },
  "error": null
}

错误响应：
{
  "success": false,
  "data": null,
  "error": {
    "code": "PERMISSION_DENIED",
    "message": "审批人 uid_010 的审批额度不足",
    "details": [ { "stepIndex": 1, "assigneeId": "uid_010", "reason": "..." } ]
  }
}

错误码：
  VALIDATION_ERROR          - 请求参数不合法
  PROXY_NOT_AUTHORIZED      - 无代提交权限
  PERMISSION_VALIDATION_FAILED - 审批人权限验证失败（含详情）
  STEP_NOT_MODIFIABLE       - 步骤不可修改（已激活或已完成）
  TASK_NOT_FOUND            - 任务不存在或无权操作
  PROCESS_ALREADY_FINISHED  - 流程已终止，不可操作
  CONCURRENT_CONFLICT       - 并发冲突（乐观锁失败，请重试）
  EXTENSION_POINT_ERROR     - 扩展点调用失败（含降级策略）
```

### 5.2 流程定义接口

```
GET    /api/v1/process-definitions
       Query: status(draft|active|archived), page, pageSize
       Response: PagedResult<ProcessDefinitionDto>

POST   /api/v1/process-definitions
       Body: CreateProcessDefinitionRequest
       Response: ProcessDefinitionDto

GET    /api/v1/process-definitions/{id}
       Response: ProcessDefinitionDto（含 nodeTemplates）

PUT    /api/v1/process-definitions/{id}
       Body: UpdateProcessDefinitionRequest
       Response: ProcessDefinitionDto

POST   /api/v1/process-definitions/{id}/activate
       Response: { "version": 2 }（激活后版本号）

POST   /api/v1/process-definitions/{id}/archive
       Response: 204 No Content
```

**CreateProcessDefinitionRequest**

```json
{
  "name": "费用报销",
  "processType": "expense_report",
  "nodeTemplates": [
    { "type": "approval", "assignees": [{ "type": "role", "value": "dept_manager" }] }
  ],
  "approverResolverUrl": "https://biz-service/workflow/resolve-approvers",
  "permissionValidatorUrl": "https://biz-service/workflow/validate-permissions"
}
```

### 5.3 提交流程接口

```
POST   /api/v1/process-instances/prepare
       用途：打开表单时调用，获取默认审批步骤列表（不创建实例）
       Body: PrepareSubmitRequest
       Response: PrepareSubmitResponse

POST   /api/v1/process-instances
       用途：正式提交流程
       Body: SubmitProcessRequest
       Response: ProcessInstanceDto

GET    /api/v1/process-instances/{id}
       Response: ProcessInstanceDetailDto（含 steps、currentStep）

GET    /api/v1/process-instances/{id}/steps
       Response: List<ApprovalStepDto>（按 stepIndex 排序）

GET    /api/v1/process-instances/{id}/history
       Response: List<TaskHistoryDto>（审批历史时间线）

POST   /api/v1/process-instances/{id}/withdraw
       用途：发起人撤回（仅 running 且未被任何人处理时可撤回）
       Response: 204 No Content

POST   /api/v1/process-instances/{id}/mark-urgent
       Body: { "reason": "项目紧急，请尽快审批" }
       Response: 204 No Content
```

**PrepareSubmitRequest**

```json
{
  "processType": "expense_report",
  "formData": { "amount": 15000, "department": "finance", "description": "出差报销" },
  "submittedBy": "uid_001",
  "onBehalfOf": "uid_002"
}
```

**PrepareSubmitResponse**

```json
{
  "defaultSteps": [
    {
      "stepIndex": 1,
      "type": "approval",
      "assignees": [{ "userId": "uid_010", "displayName": "张部长" }]
    },
    {
      "stepIndex": 2,
      "type": "joint_sign",
      "assignees": [
        { "userId": "uid_011", "displayName": "李CFO" },
        { "userId": "uid_012", "displayName": "王审计" }
      ],
      "jointSignPolicy": "ALL_PASS"
    }
  ],
  "metadata": {}
}
```

**SubmitProcessRequest**

```json
{
  "processType": "expense_report",
  "businessKey": "EXP-2026-001234",
  "formData": { "amount": 15000, "department": "finance" },
  "submittedBy": "uid_001",
  "onBehalfOf": "uid_002",
  "confirmedSteps": [
    {
      "stepIndex": 1,
      "type": "approval",
      "assignees": [{ "userId": "uid_010" }]
    },
    {
      "stepIndex": 1.5,
      "type": "approval",
      "assignees": [{ "userId": "uid_015" }],
      "comment": "用户新增：财务总监二次审核"
    },
    {
      "stepIndex": 2,
      "type": "joint_sign",
      "assignees": [{ "userId": "uid_011" }, { "userId": "uid_012" }],
      "jointSignPolicy": "ALL_PASS"
    }
  ]
}
```

### 5.4 任务操作接口

```
GET    /api/v1/tasks/pending
       Query: page, pageSize, isUrgent(boolean)
       Response: PagedResult<PendingTaskDto>

GET    /api/v1/tasks/completed
       Query: page, pageSize, startDate, endDate
       Response: PagedResult<CompletedTaskDto>

GET    /api/v1/tasks/{id}
       Response: TaskDetailDto（含流程信息、步骤链）

POST   /api/v1/tasks/{id}/approve
       Body: { "comment": "同意" }
       Response: 204 No Content

POST   /api/v1/tasks/{id}/reject
       Body: { "comment": "金额超出预算，请重新申请" }  （comment 必填）
       Response: 204 No Content

POST   /api/v1/tasks/{id}/return
       Body: { "targetStepId": "uuid-or-null", "comment": "材料不完整" }
       Note: targetStepId 为 null 时退回发起人；指定 ID 时退回该步骤
       Response: 204 No Content

POST   /api/v1/tasks/{id}/countersign
       用途：加签（在当前步骤后插入新步骤）
       Body: CountersignRequest
       Response: { "newStepId": "uuid" }

POST   /api/v1/tasks/{id}/notify-read
       用途：通知类任务标记已读
       Response: 204 No Content
```

**CountersignRequest**

```json
{
  "assignees": [{ "userId": "uid_020" }],
  "comment": "需要风控部门额外审批"
}
```

### 5.5 动态审批人管理接口

```
GET    /api/v1/process-instances/{id}/steps/{stepId}/assignees
       Response: List<AssigneeDto>

PUT    /api/v1/process-instances/{id}/steps/{stepId}/assignees
       用途：替换某步骤的审批人（步骤须为 pending 状态）
       Body: { "assignees": [{ "userId": "uid_030" }] }
       Response: ApprovalStepDto

POST   /api/v1/process-instances/{id}/steps
       用途：插入新步骤
       Body: InsertStepRequest
       Response: ApprovalStepDto（含分配的 stepIndex）

DELETE /api/v1/process-instances/{id}/steps/{stepId}
       用途：删除步骤（步骤须为 pending 状态）
       Response: 204 No Content

PUT    /api/v1/process-instances/{id}/steps/reorder
       用途：调整步骤顺序（批量重设 stepIndex）
       Body: { "orderedStepIds": ["uuid1", "uuid2", "uuid3"] }
       Response: List<ApprovalStepDto>（更新后的步骤列表）

GET    /api/v1/process-instances/{id}/modifications
       Response: List<ApproverModificationDto>（修改历史）
```

**InsertStepRequest**

```json
{
  "afterStepId": "uuid-of-step-1",
  "type": "approval",
  "assignees": [{ "userId": "uid_030" }],
  "comment": "增加财务复核"
}
```

### 5.6 代理配置接口

```
GET    /api/v1/proxy-configs
       Query: agentId, principalId, isActive
       Response: List<ProxyConfigDto>

POST   /api/v1/proxy-configs
       Body: CreateProxyConfigRequest
       Response: ProxyConfigDto

DELETE /api/v1/proxy-configs/{id}
       Response: 204 No Content

GET    /api/v1/proxy-configs/my-principals
       用途：获取当前用户可代理的人列表
       Response: List<PrincipalDto>（含 principalId、displayName）
```

### 5.7 委托配置接口

```
GET    /api/v1/delegation-configs
       Query: delegatorId, isActive
       Response: List<DelegationConfigDto>

POST   /api/v1/delegation-configs
       Body: CreateDelegationRequest
       Response: DelegationConfigDto

PUT    /api/v1/delegation-configs/{id}
       Body: UpdateDelegationRequest（更新有效期、委托范围）
       Response: DelegationConfigDto

DELETE /api/v1/delegation-configs/{id}
       Response: 204 No Content
```

### 5.8 审批规则接口

```
GET    /api/v1/approval-rules
       Query: processType, isActive, page, pageSize
       Response: PagedResult<ApprovalRuleDto>

POST   /api/v1/approval-rules
       Body: CreateApprovalRuleRequest
       Response: ApprovalRuleDto

PUT    /api/v1/approval-rules/{id}
       Body: UpdateApprovalRuleRequest
       Response: ApprovalRuleDto

DELETE /api/v1/approval-rules/{id}
       Response: 204 No Content

POST   /api/v1/approval-rules/test
       用途：测试规则是否命中（调试用）
       Body: { "processType": "expense_report", "formData": { "amount": 15000 } }
       Response: { "matchedRuleId": "uuid", "resultSteps": [...] }
```

---

## 六、流程定义模块

### 6.1 版本管理策略

- 流程定义采用**不可变版本**原则：激活后不允许修改，需新建版本。
- 进行中的流程实例绑定启动时的 `definitionVersion`，不受新版本影响。
- 同一 `processType` 只有一个 `active` 版本，激活新版本时旧版本自动归档。

```csharp
// Application/Commands/ProcessDefinition/ActivateDefinitionHandler.cs
public class ActivateDefinitionHandler : IRequestHandler<ActivateDefinitionCommand, int>
{
    public async Task<int> Handle(ActivateDefinitionCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);

        // 归档同 processType 的当前 active 版本
        var current = await _uow.ProcessDefinitions
            .GetActiveByProcessTypeAsync(cmd.ProcessType, ct);
        if (current != null)
        {
            current.Status = DefinitionStatus.Archived;
            _uow.ProcessDefinitions.Update(current);
        }

        // 激活新版本
        var definition = await _uow.ProcessDefinitions.GetByIdAsync(cmd.DefinitionId, ct)
            ?? throw new NotFoundException("ProcessDefinition", cmd.DefinitionId);
        definition.Status = DefinitionStatus.Active;
        _uow.ProcessDefinitions.Update(definition);

        await _uow.CommitAsync(ct);
        return definition.Version;
    }
}
```

---

## 七、提交流程实现

### 7.1 完整提交 Handler

```csharp
// Application/Commands/Submission/SubmitProcessHandler.cs
public class SubmitProcessHandler : IRequestHandler<SubmitProcessCommand, ProcessInstanceDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IApproverResolver _approverResolver;
    private readonly IPermissionValidator _permissionValidator;
    private readonly INotificationPublisher _notifications;
    private readonly IMediator _mediator;

    public async Task<ProcessInstanceDto> Handle(SubmitProcessCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);

        // Step 1: 获取激活的流程定义
        var definition = await _uow.ProcessDefinitions
            .GetActiveByProcessTypeAsync(cmd.ProcessType, ct)
            ?? throw new WorkflowException($"流程类型 {cmd.ProcessType} 不存在或未激活");

        // Step 2: 代提交权限校验
        if (cmd.SubmittedBy != cmd.OnBehalfOf && cmd.OnBehalfOf != null)
        {
            var hasProxy = await ValidateProxyPermissionAsync(
                cmd.SubmittedBy, cmd.OnBehalfOf, cmd.ProcessType, ct);
            if (!hasProxy)
                throw new PermissionDeniedException($"{cmd.SubmittedBy} 无权代 {cmd.OnBehalfOf} 提交");
        }

        // Step 3: 解析原始默认审批人列表（用于 diff 记录，不再调用，由前端 prepare 时已获取）
        //         confirmedSteps 是用户已确认的最终列表
        var originalSteps = await _approverResolver.ResolveAsync(
            new ResolveApproversContext(cmd.ProcessType, cmd.FormData, cmd.SubmittedBy, cmd.OnBehalfOf,
                definition.ApproverResolverUrl), ct);

        // Step 4: 计算 diff，生成修改记录
        var diff = DiffCalculator.Calculate(originalSteps.Steps, cmd.ConfirmedSteps);

        // Step 5: 权限验证（调用扩展点）
        var validationResult = await _permissionValidator.ValidateAsync(
            new ValidatePermissionsContext(
                cmd.ProcessType, cmd.FormData, cmd.SubmittedBy,
                originalSteps.Steps, cmd.ConfirmedSteps,
                diff.Any(), definition.PermissionValidatorUrl), ct);

        if (!validationResult.Passed)
            throw new PermissionValidationFailedException(validationResult.FailedItems, validationResult.Message);

        // Step 6: 委托透明替换
        var stepsAfterDelegation = await ApplyDelegationAsync(cmd.ConfirmedSteps, cmd.ProcessType, ct);

        // Step 7: 创建流程实例
        var instance = new ProcessInstance
        {
            Id                = Guid.NewGuid(),
            DefinitionId      = definition.Id,
            DefinitionVersion = definition.Version,
            BusinessKey       = cmd.BusinessKey,
            FormDataSnapshot  = cmd.FormData,
            SubmittedBy       = cmd.SubmittedBy,
            OnBehalfOf        = cmd.OnBehalfOf,
            Status            = ProcessStatus.Running,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
        };
        _uow.ProcessInstances.Add(instance);

        // Step 8: 创建 ApprovalStep 列表
        var steps = stepsAfterDelegation.Select(s => new ApprovalStep
        {
            Id             = Guid.NewGuid(),
            InstanceId     = instance.Id,
            StepIndex      = s.StepIndex,
            Type           = s.Type,
            Assignees      = s.Assignees,
            JointSignPolicy= s.JointSignPolicy,
            Status         = StepStatus.Pending,
            Source         = StepSource.Original,
        }).OrderBy(s => s.StepIndex).ToList();

        foreach (var step in steps) _uow.ApprovalSteps.Add(step);

        // Step 9: 保存审批人修改记录
        var modification = new ApproverListModification
        {
            Id            = Guid.NewGuid(),
            InstanceId    = instance.Id,
            ModifiedBy    = cmd.SubmittedBy,
            ModifiedAt    = DateTime.UtcNow,
            OriginalSteps = JsonSerializer.Serialize(originalSteps.Steps),
            FinalSteps    = JsonSerializer.Serialize(cmd.ConfirmedSteps),
            DiffSummary   = JsonSerializer.Serialize(diff),
        };
        _uow.ApproverModifications.Add(modification);

        // Step 10: 激活第一个步骤，生成 Task
        var firstStep = steps.First();
        firstStep.Status = StepStatus.Active;
        instance.CurrentStepIndex = firstStep.StepIndex;
        var tasks = CreateTasksForStep(firstStep, instance);
        foreach (var task in tasks) _uow.Tasks.Add(task);

        await _uow.CommitAsync(ct);

        // Step 11: 发布领域事件（异步通知）
        await _mediator.Publish(new ProcessSubmittedEvent(instance.Id, tasks), ct);

        return instance.ToDto();
    }

    private List<WorkflowTask> CreateTasksForStep(ApprovalStep step, ProcessInstance instance)
    {
        return step.Assignees.Select(a => new WorkflowTask
        {
            Id                  = Guid.NewGuid(),
            InstanceId          = instance.Id,
            StepId              = step.Id,
            AssigneeId          = a.UserId,
            OriginalAssigneeId  = a.OriginalUserId,     // 委托替换时保留原始人
            IsDelegated         = a.IsDelegated,
            Status              = TaskStatus.Pending,
            IsUrgent            = instance.IsUrgent,
            CreatedAt           = DateTime.UtcNow,
        }).ToList();
    }

    private async Task<bool> ValidateProxyPermissionAsync(
        string agentId, string principalId, string processType, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var configs = await _uow.ProxyConfigs.FindActiveAsync(agentId, principalId, now, ct);
        return configs.Any(c =>
            !c.AllowedProcessTypes.Any() ||
            c.AllowedProcessTypes.Contains(processType));
    }

    private async Task<List<ConfirmedStep>> ApplyDelegationAsync(
        List<ConfirmedStep> steps, string processType, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var allAssigneeIds = steps.SelectMany(s => s.Assignees.Select(a => a.UserId)).Distinct().ToList();
        var delegations = await _uow.DelegationConfigs
            .GetActiveForUsersAsync(allAssigneeIds, processType, now, ct);

        var delegationMap = delegations
            .GroupBy(d => d.DelegatorId)
            .ToDictionary(g => g.Key, g => g.First());

        return steps.Select(step => new ConfirmedStep
        {
            StepIndex       = step.StepIndex,
            Type            = step.Type,
            JointSignPolicy = step.JointSignPolicy,
            Assignees       = step.Assignees.Select(a =>
            {
                if (delegationMap.TryGetValue(a.UserId, out var delegation))
                {
                    return a with
                    {
                        OriginalUserId = a.UserId,
                        UserId         = delegation.DelegateeId,
                        IsDelegated    = true,
                    };
                }
                return a;
            }).ToList(),
        }).ToList();
    }
}
```

### 7.2 DiffCalculator

```csharp
// Application/Commands/Submission/DiffCalculator.cs
public static class DiffCalculator
{
    public static List<DiffEntry> Calculate(
        List<ResolvedStep> original,
        List<ConfirmedStep> confirmed)
    {
        var result = new List<DiffEntry>();

        // 简化 diff：对比每个 stepIndex 处的 assignees
        var originalByIndex = original.ToDictionary(s => s.StepIndex);
        var confirmedByIndex = confirmed.ToDictionary(s => s.StepIndex);

        // 新增的步骤
        foreach (var (index, step) in confirmedByIndex)
        {
            if (!originalByIndex.ContainsKey(index))
            {
                foreach (var assignee in step.Assignees)
                    result.Add(new DiffEntry("added", index, assignee.UserId, null));
            }
        }

        // 删除的步骤
        foreach (var (index, step) in originalByIndex)
        {
            if (!confirmedByIndex.ContainsKey(index))
            {
                foreach (var assignee in step.Assignees)
                    result.Add(new DiffEntry("removed", index, null, assignee.UserId));
            }
        }

        // 相同 index 但 assignees 有变化
        foreach (var (index, confirmedStep) in confirmedByIndex)
        {
            if (!originalByIndex.TryGetValue(index, out var originalStep)) continue;
            var origIds = originalStep.Assignees.Select(a => a.UserId).ToHashSet();
            var confIds = confirmedStep.Assignees.Select(a => a.UserId).ToHashSet();

            foreach (var id in confIds.Except(origIds))
                result.Add(new DiffEntry("added", index, id, null));
            foreach (var id in origIds.Except(confIds))
                result.Add(new DiffEntry("removed", index, null, id));
        }

        return result;
    }
}

public record DiffEntry(string Action, decimal StepIndex, string? AssigneeId, string? RemovedAssigneeId);
```

---

## 八、审批操作实现

### 8.1 通过（Approve）Handler

```csharp
// Application/Commands/Approval/ApproveTaskHandler.cs
public class ApproveTaskHandler : IRequestHandler<ApproveTaskCommand>
{
    public async Task Handle(ApproveTaskCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);

        var task = await _uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new TaskNotFoundException(cmd.TaskId);

        EnsureTaskIsActionable(task, cmd.CurrentUserId);

        // 完成当前任务
        task.Status      = TaskStatus.Completed;
        task.Action      = TaskAction.Approve;
        task.Comment     = cmd.Comment;
        task.CompletedAt = DateTime.UtcNow;
        _uow.Tasks.Update(task);

        // 检查当前步骤是否可推进
        var step = await _uow.ApprovalSteps.GetByIdAsync(task.StepId, ct)!;
        var stepTasks = await _uow.Tasks.GetActiveTasksByStepAsync(step.Id, ct);
        var canAdvance = CanAdvanceStep(step, stepTasks);

        if (canAdvance)
        {
            // 跳过该步骤中其他 pending 任务（ANY_ONE 场景）
            foreach (var t in stepTasks.Where(t => t.Status == TaskStatus.Pending && t.Id != task.Id))
            {
                t.Status = TaskStatus.Skipped;
                _uow.Tasks.Update(t);
            }

            step.Status      = StepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;
            _uow.ApprovalSteps.Update(step);

            // 激活下一步骤
            var instance = await _uow.ProcessInstances.GetByIdAsync(step.InstanceId, ct)!;
            await AdvanceToNextStepAsync(instance, step, ct);
        }

        await _uow.CommitAsync(ct);
        await _mediator.Publish(new TaskCompletedEvent(task.Id, task.InstanceId), ct);
    }

    private bool CanAdvanceStep(ApprovalStep step, List<WorkflowTask> tasks)
    {
        var completedCount = tasks.Count(t => t.Status == TaskStatus.Completed);
        var totalCount     = tasks.Count(t => t.Status != TaskStatus.Skipped);

        return step.Type switch
        {
            StepType.Approval  => true,  // 普通审批，当前人完成即推进
            StepType.Notify    => true,  // 通知，立即推进
            StepType.JointSign => step.JointSignPolicy switch
            {
                JointSignPolicy.AnyOne   => completedCount >= 1,
                JointSignPolicy.Majority => completedCount > totalCount / 2.0,
                JointSignPolicy.AllPass  => completedCount == totalCount,
                _                        => false,
            },
            _ => false,
        };
    }

    private async Task AdvanceToNextStepAsync(ProcessInstance instance, ApprovalStep currentStep, CancellationToken ct)
    {
        var allSteps = await _uow.ApprovalSteps.GetByInstanceAsync(instance.Id, ct);
        var nextStep = allSteps
            .Where(s => s.StepIndex > currentStep.StepIndex && s.Status == StepStatus.Pending)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault();

        if (nextStep == null)
        {
            // 无下一步，流程完成
            instance.Status      = ProcessStatus.Completed;
            instance.CompletedAt = DateTime.UtcNow;
            _uow.ProcessInstances.Update(instance);
            await _mediator.Publish(new ProcessCompletedEvent(instance.Id), ct);
            return;
        }

        // 激活下一步
        nextStep.Status = StepStatus.Active;
        instance.CurrentStepIndex = nextStep.StepIndex;
        _uow.ApprovalSteps.Update(nextStep);
        _uow.ProcessInstances.Update(instance);

        // 生成下一步任务（含委托检查）
        var processType = (await _uow.ProcessDefinitions.GetByIdAsync(instance.DefinitionId, ct))!.ProcessType;
        var stepsWithDelegation = await ApplyDelegationToAssigneesAsync(nextStep, processType, ct);
        var newTasks = CreateTasksForStep(stepsWithDelegation, instance);
        foreach (var t in newTasks) _uow.Tasks.Add(t);

        await _mediator.Publish(new StepActivatedEvent(instance.Id, nextStep.Id, newTasks), ct);
    }
}
```

### 8.2 驳回（Reject）Handler

```csharp
public class RejectTaskHandler : IRequestHandler<RejectTaskCommand>
{
    public async Task Handle(RejectTaskCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);

        var task = await _uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new TaskNotFoundException(cmd.TaskId);
        EnsureTaskIsActionable(task, cmd.CurrentUserId);

        if (string.IsNullOrWhiteSpace(cmd.Comment))
            throw new ValidationException("驳回必须填写原因");

        // 完成当前任务
        task.Status      = TaskStatus.Rejected;
        task.Action      = TaskAction.Reject;
        task.Comment     = cmd.Comment;
        task.CompletedAt = DateTime.UtcNow;
        _uow.Tasks.Update(task);

        // 终止流程：将所有 pending/active 步骤标记为 skipped
        var instance = await _uow.ProcessInstances.GetByIdAsync(task.InstanceId, ct)!;
        var activeSteps = await _uow.ApprovalSteps
            .GetByStatusAsync(instance.Id, [StepStatus.Pending, StepStatus.Active], ct);

        foreach (var step in activeSteps)
        {
            step.Status = StepStatus.Skipped;
            _uow.ApprovalSteps.Update(step);
            // 同时跳过该步骤的所有 pending 任务
            var pendingTasks = await _uow.Tasks.GetActiveTasksByStepAsync(step.Id, ct);
            foreach (var t in pendingTasks.Where(t => t.Id != task.Id))
            {
                t.Status = TaskStatus.Skipped;
                _uow.Tasks.Update(t);
            }
        }

        instance.Status      = ProcessStatus.Rejected;
        instance.CompletedAt = DateTime.UtcNow;
        _uow.ProcessInstances.Update(instance);

        await _uow.CommitAsync(ct);
        await _mediator.Publish(new ProcessRejectedEvent(instance.Id, task.AssigneeId, cmd.Comment), ct);
    }
}
```

### 8.3 退回（Return）Handler

```csharp
public class ReturnTaskHandler : IRequestHandler<ReturnTaskCommand>
{
    public async Task Handle(ReturnTaskCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);

        var task = await _uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new TaskNotFoundException(cmd.TaskId);
        EnsureTaskIsActionable(task, cmd.CurrentUserId);

        task.Status      = TaskStatus.Returned;
        task.Action      = TaskAction.Return;
        task.Comment     = cmd.Comment;
        task.CompletedAt = DateTime.UtcNow;
        _uow.Tasks.Update(task);

        var instance = await _uow.ProcessInstances.GetWithStepsAsync(task.InstanceId, ct)!;
        var currentStep = instance.ApprovalSteps.First(s => s.Id == task.StepId);

        ApprovalStep targetStep;
        if (cmd.TargetStepId.HasValue)
        {
            // 退回到指定步骤
            targetStep = instance.ApprovalSteps.FirstOrDefault(s => s.Id == cmd.TargetStepId.Value)
                ?? throw new StepNotFoundException(cmd.TargetStepId.Value);
        }
        else
        {
            // 退回发起人（step index 最小的步骤）
            targetStep = instance.ApprovalSteps.OrderBy(s => s.StepIndex).First();
        }

        // 将当前步骤到目标步骤之间的所有步骤 active→pending（重置），跳过中间任务
        var stepsToReset = instance.ApprovalSteps
            .Where(s => s.StepIndex >= targetStep.StepIndex && s.StepIndex < currentStep.StepIndex)
            .ToList();

        foreach (var step in stepsToReset)
        {
            step.Status      = StepStatus.Pending;
            step.CompletedAt = null;
            _uow.ApprovalSteps.Update(step);
        }

        // 当前步骤中其他 pending 任务跳过
        var siblingTasks = await _uow.Tasks.GetActiveTasksByStepAsync(currentStep.Id, ct);
        foreach (var t in siblingTasks.Where(t => t.Id != task.Id && t.Status == TaskStatus.Pending))
        {
            t.Status = TaskStatus.Skipped;
            _uow.Tasks.Update(t);
        }

        // 激活目标步骤，重新生成任务
        targetStep.Status = StepStatus.Active;
        instance.CurrentStepIndex = targetStep.StepIndex;
        _uow.ApprovalSteps.Update(targetStep);
        _uow.ProcessInstances.Update(instance);

        var processType = (await _uow.ProcessDefinitions.GetByIdAsync(instance.DefinitionId, ct))!.ProcessType;
        var newTasks = await CreateTasksWithDelegationAsync(targetStep, instance, processType, ct);
        foreach (var t in newTasks) _uow.Tasks.Add(t);

        await _uow.CommitAsync(ct);
        await _mediator.Publish(new TaskReturnedEvent(instance.Id, targetStep.Id, newTasks), ct);
    }
}
```

### 8.4 加签（Countersign）Handler

```csharp
public class CountersignTaskHandler : IRequestHandler<CountersignTaskCommand, Guid>
{
    public async Task<Guid> Handle(CountersignTaskCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);

        var task = await _uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new TaskNotFoundException(cmd.TaskId);
        EnsureTaskIsActionable(task, cmd.CurrentUserId);

        var currentStep = await _uow.ApprovalSteps.GetByIdAsync(task.StepId, ct)!;
        var instance    = await _uow.ProcessInstances.GetWithStepsAsync(currentStep.InstanceId, ct)!;

        // 找到当前步骤的下一步，计算插入 index
        var nextStep = instance.ApprovalSteps
            .Where(s => s.StepIndex > currentStep.StepIndex && s.Status == StepStatus.Pending)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault();

        decimal newIndex = nextStep == null
            ? currentStep.StepIndex + 1         // 当前是最后一步，直接 +1
            : (currentStep.StepIndex + nextStep.StepIndex) / 2; // 取中间值

        // 检查 index 是否已存在（极端情况：多次加签导致精度不足）
        if (instance.ApprovalSteps.Any(s => s.StepIndex == newIndex))
            throw new WorkflowException("步骤索引冲突，请重新操作");

        var newStep = new ApprovalStep
        {
            Id              = Guid.NewGuid(),
            InstanceId      = instance.Id,
            StepIndex       = newIndex,
            Type            = StepType.Approval,
            Assignees       = cmd.Assignees.Select(a => new StepAssignee(a.UserId)).ToList(),
            Status          = StepStatus.Pending,
            Source          = StepSource.Countersign,
            AddedByUserId   = cmd.CurrentUserId,
            AddedAt         = DateTime.UtcNow,
        };
        _uow.ApprovalSteps.Add(newStep);

        // 当前任务标记加签动作，但步骤仍需完成正常审批流转
        task.Action  = TaskAction.Countersign;
        task.Comment = cmd.Comment;
        _uow.Tasks.Update(task);

        await _uow.CommitAsync(ct);
        return newStep.Id;
    }
}
```

---

## 九、动态审批人管理

### 9.1 插入步骤

插入步骤时需要：
1. 校验操作人权限（发起人或管理员）
2. 新步骤只能插在当前步骤之后（不能插到已完成或已激活步骤之前）
3. 记录修改历史

```csharp
public class InsertStepHandler : IRequestHandler<InsertStepCommand, ApprovalStepDto>
{
    public async Task<ApprovalStepDto> Handle(InsertStepCommand cmd, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetWithStepsAsync(cmd.InstanceId, ct)
            ?? throw new NotFoundException("ProcessInstance", cmd.InstanceId);

        EnsureCanModifyInstance(instance, cmd.OperatorId);

        var afterStep = instance.ApprovalSteps.FirstOrDefault(s => s.Id == cmd.AfterStepId)
            ?? throw new StepNotFoundException(cmd.AfterStepId);

        // 确保插入位置在当前激活步骤之后
        if (afterStep.StepIndex < instance.CurrentStepIndex)
            throw new WorkflowException("不能在当前步骤之前插入新步骤");

        // 计算新 stepIndex
        var nextStep = instance.ApprovalSteps
            .Where(s => s.StepIndex > afterStep.StepIndex)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault();

        decimal newIndex = nextStep == null
            ? afterStep.StepIndex + 1
            : (afterStep.StepIndex + nextStep.StepIndex) / 2;

        await _uow.BeginTransactionAsync(ct);

        var newStep = new ApprovalStep
        {
            Id          = Guid.NewGuid(),
            InstanceId  = instance.Id,
            StepIndex   = newIndex,
            Type        = cmd.Type,
            Assignees   = cmd.Assignees,
            Status      = StepStatus.Pending,
            Source      = StepSource.DynamicAdded,
            AddedByUserId = cmd.OperatorId,
            AddedAt     = DateTime.UtcNow,
        };
        _uow.ApprovalSteps.Add(newStep);

        // 追加修改记录
        await AppendModificationAsync(instance, cmd.OperatorId, "step_inserted", newStep, ct);

        await _uow.CommitAsync(ct);
        return newStep.ToDto();
    }
}
```

### 9.2 调整步骤顺序

```csharp
public class ReorderStepsHandler : IRequestHandler<ReorderStepsCommand, List<ApprovalStepDto>>
{
    public async Task<List<ApprovalStepDto>> Handle(ReorderStepsCommand cmd, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetWithStepsAsync(cmd.InstanceId, ct)
            ?? throw new NotFoundException("ProcessInstance", cmd.InstanceId);

        EnsureCanModifyInstance(instance, cmd.OperatorId);

        // 只能对 pending 步骤重排，且不能跨越当前激活步骤
        var pendingSteps = instance.ApprovalSteps
            .Where(s => s.Status == StepStatus.Pending)
            .ToDictionary(s => s.Id);

        // 验证所有传入的 stepId 都是 pending 步骤
        foreach (var stepId in cmd.OrderedStepIds)
        {
            if (!pendingSteps.ContainsKey(stepId))
                throw new WorkflowException($"步骤 {stepId} 不可调整顺序（已激活或已完成）");
        }

        await _uow.BeginTransactionAsync(ct);

        // 获取当前最大已完成 stepIndex，新顺序必须在其之后
        var maxCompletedIndex = instance.ApprovalSteps
            .Where(s => s.Status is StepStatus.Completed or StepStatus.Active)
            .Select(s => s.StepIndex)
            .DefaultIfEmpty(0)
            .Max();

        // 重新分配 stepIndex：从 maxCompletedIndex + 1 开始，间隔 1
        decimal baseIndex = Math.Floor(maxCompletedIndex) + 1;
        for (int i = 0; i < cmd.OrderedStepIds.Count; i++)
        {
            var step = pendingSteps[cmd.OrderedStepIds[i]];
            step.StepIndex = baseIndex + i;
            _uow.ApprovalSteps.Update(step);
        }

        await AppendModificationAsync(instance, cmd.OperatorId, "steps_reordered", null, ct);
        await _uow.CommitAsync(ct);

        return instance.ApprovalSteps.OrderBy(s => s.StepIndex).Select(s => s.ToDto()).ToList();
    }
}
```

---

## 十、代理与委托模块

### 10.1 代理配置管理

```csharp
// 获取当前用户可代理的人列表
public class GetMyPrincipalsHandler : IRequestHandler<GetMyPrincipalsQuery, List<PrincipalDto>>
{
    public async Task<List<PrincipalDto>> Handle(GetMyPrincipalsQuery query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var configs = await _uow.ProxyConfigs.GetActiveByAgentAsync(query.AgentId, now, ct);

        return configs.Select(c => new PrincipalDto
        {
            UserId             = c.PrincipalId,
            AllowedProcessTypes = c.AllowedProcessTypes,
            ValidTo            = c.ValidTo,
        }).ToList();
    }
}
```

### 10.2 委托配置查询（批量）

提交流程时批量查询委托配置，减少数据库查询次数：

```csharp
// Infrastructure/Repositories/DelegationConfigRepository.cs
public class DelegationConfigRepository : IDelegationConfigRepository
{
    public async Task<List<DelegationConfig>> GetActiveForUsersAsync(
        List<string> userIds, string processType, DateTime now, CancellationToken ct)
    {
        return await _ctx.DelegationConfigs
            .Where(d =>
                d.IsActive &&
                userIds.Contains(d.DelegatorId) &&
                d.ValidFrom <= now && d.ValidTo >= now &&
                // 空数组=全部流程，或包含指定类型
                (d.AllowedProcessTypes == null ||
                 d.AllowedProcessTypes.Count == 0 ||
                 d.AllowedProcessTypes.Contains(processType)))
            .ToListAsync(ct);
    }
}
```

---

## 十一、任务列表模块

### 11.1 待办任务查询（带分页和过滤）

```csharp
// Application/Queries/Tasks/GetPendingTasksHandler.cs
public class GetPendingTasksHandler : IRequestHandler<GetPendingTasksQuery, PagedResult<PendingTaskDto>>
{
    public async Task<PagedResult<PendingTaskDto>> Handle(GetPendingTasksQuery query, CancellationToken ct)
    {
        return await _uow.Tasks.GetPendingTasksAsync(
            query.UserId, query.Page, query.PageSize, ct);
    }
}

// Infrastructure/Repositories/TaskRepository.cs
public async Task<PagedResult<PendingTaskDto>> GetPendingTasksAsync(
    string userId, int page, int pageSize, CancellationToken ct)
{
    var q = _ctx.Tasks
        .Where(t => t.AssigneeId == userId && t.Status == TaskStatus.Pending)
        .Join(_ctx.ProcessInstances, t => t.InstanceId, pi => pi.Id, (t, pi) => new { t, pi })
        .Join(_ctx.ProcessDefinitions.Where(pd => pd.Status == DefinitionStatus.Active),
              x => x.pi.DefinitionId, pd => pd.Id, (x, pd) => new { x.t, x.pi, pd })
        .Select(x => new PendingTaskDto
        {
            TaskId          = x.t.Id,
            InstanceId      = x.t.InstanceId,
            ProcessName     = x.pd.Name,
            BusinessKey     = x.pi.BusinessKey,
            InitiatorId     = x.pi.OnBehalfOf ?? x.pi.SubmittedBy,
            IsUrgent        = x.t.IsUrgent,
            IsDelegated     = x.t.IsDelegated,
            OriginalAssigneeId = x.t.OriginalAssigneeId,
            PendingSince    = x.t.CreatedAt,
            FormSummary     = x.pi.FormDataSnapshot,   // 前端自行提取摘要字段
        })
        .OrderByDescending(x => x.IsUrgent)
        .ThenBy(x => x.PendingSince);

    var total = await q.CountAsync(ct);
    var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    return new PagedResult<PendingTaskDto>(items, total, page, pageSize);
}
```

### 11.2 已办任务查询

```csharp
public async Task<PagedResult<CompletedTaskDto>> GetCompletedTasksAsync(
    string userId, int page, int pageSize, CancellationToken ct)
{
    // 包含：自己处理的 + 委托他人但原始审批人是自己的
    var q = _ctx.Tasks
        .Where(t =>
            (t.AssigneeId == userId || t.OriginalAssigneeId == userId) &&
            (t.Status == TaskStatus.Completed ||
             t.Status == TaskStatus.Returned ||
             t.Status == TaskStatus.Rejected))
        .Join(_ctx.ProcessInstances, t => t.InstanceId, pi => pi.Id, (t, pi) => new { t, pi })
        .Join(_ctx.ProcessDefinitions,
              x => x.pi.DefinitionId, pd => pd.Id, (x, pd) => new { x.t, x.pi, pd })
        .Select(x => new CompletedTaskDto
        {
            TaskId          = x.t.Id,
            InstanceId      = x.t.InstanceId,
            ProcessName     = x.pd.Name,
            BusinessKey     = x.pi.BusinessKey,
            Action          = x.t.Action,
            Comment         = x.t.Comment,
            IsDelegated     = x.t.IsDelegated,
            OriginalAssigneeId = x.t.OriginalAssigneeId,
            ProcessStatus   = x.pi.Status,
            CompletedAt     = x.t.CompletedAt,
        })
        .OrderByDescending(x => x.CompletedAt);

    var total = await q.CountAsync(ct);
    var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    return new PagedResult<CompletedTaskDto>(items, total, page, pageSize);
}
```

---

## 十二、扩展点集成实现

### 12.1 接口定义（Application 层）

```csharp
// Application/Services/IApproverResolver.cs
public interface IApproverResolver
{
    Task<ResolveApproversResult> ResolveAsync(
        ResolveApproversContext context, CancellationToken ct = default);
}

public record ResolveApproversContext(
    string ProcessType,
    Dictionary<string, object> FormData,
    string SubmittedBy,
    string? OnBehalfOf,
    string? CallbackUrl);

public record ResolveApproversResult(List<ResolvedStep> Steps, Dictionary<string, object>? Metadata);

// Application/Services/IPermissionValidator.cs
public interface IPermissionValidator
{
    Task<ValidatePermissionsResult> ValidateAsync(
        ValidatePermissionsContext context, CancellationToken ct = default);
}

public record ValidatePermissionsResult(
    bool Passed,
    List<PermissionFailItem> FailedItems,
    string Message);

public record PermissionFailItem(decimal StepIndex, string AssigneeId, string Reason);
```

### 12.2 HTTP Callback 实现（带 Polly 弹性）

```csharp
// Infrastructure/ExternalServices/HttpApproverResolver.cs
public class HttpApproverResolver : IApproverResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpApproverResolver> _logger;

    public async Task<ResolveApproversResult> ResolveAsync(
        ResolveApproversContext ctx, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ctx.CallbackUrl))
        {
            // 无配置回调：返回空列表，由提交人手动指定
            return new ResolveApproversResult([], null);
        }

        var client = _httpClientFactory.CreateClient("WorkflowExtension");
        var request = new
        {
            processType  = ctx.ProcessType,
            formData     = ctx.FormData,
            submittedBy  = ctx.SubmittedBy,
            onBehalfOf   = ctx.OnBehalfOf,
            requestId    = Guid.NewGuid().ToString("N"),
        };

        try
        {
            var response = await client.PostAsJsonAsync(ctx.CallbackUrl, request, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ExternalResolveResponse>(ct: ct)
                ?? throw new WorkflowException("扩展点返回数据格式错误");

            return new ResolveApproversResult(
                result.Steps.Select(MapStep).ToList(),
                result.Metadata);
        }
        catch (HttpRequestException ex) when (IsTimeout(ex))
        {
            _logger.LogWarning("ApproverResolver 超时，降级为空审批人列表. Url={Url}", ctx.CallbackUrl);
            // 降级策略：返回空列表，让提交人手动填写
            return new ResolveApproversResult([], null);
        }
    }
}

// Program.cs 中注册 HTTP 客户端弹性策略
builder.Services
    .AddHttpClient("WorkflowExtension")
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(2, retryAttempt =>
            TimeSpan.FromMilliseconds(300 * retryAttempt)))  // 重试 2 次
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(
        TimeSpan.FromSeconds(5)));  // 5 秒超时
```

### 12.3 SPI 插件方式（同进程部署）

```csharp
// 业务系统在自己的 Startup 中直接注册实现
// 覆盖 Infrastructure 中的 HTTP 实现

public class BizApproverResolver : IApproverResolver
{
    private readonly IExpenseApprovalService _expenseService;

    public async Task<ResolveApproversResult> ResolveAsync(
        ResolveApproversContext ctx, CancellationToken ct)
    {
        // 直接调用业务层服务，无 HTTP 开销
        var rules = await _expenseService.GetApprovalRulesAsync(ctx.FormData, ct);
        return new ResolveApproversResult(rules.ToSteps(), null);
    }
}

// services.AddScoped<IApproverResolver, BizApproverResolver>();  // 覆盖默认 HTTP 实现
```

---

## 十三、通知服务实现

### 13.1 通知事件设计（MassTransit 消息）

```csharp
// Application/Messages/WorkflowNotification.cs
public record WorkflowNotification
{
    public Guid   InstanceId   { get; init; }
    public string EventType    { get; init; } = "";  // task_assigned | process_completed | process_rejected | urgent_marked | task_returned
    public string RecipientId  { get; init; } = "";
    public string ProcessName  { get; init; } = "";
    public string BusinessKey  { get; init; } = "";
    public string? Message     { get; init; }
    public bool   IsUrgent     { get; init; }
    public Dictionary<string, object> ExtraData { get; init; } = [];
}
```

### 13.2 领域事件 → 消息队列发布

```csharp
// Application/EventHandlers/ProcessSubmittedEventHandler.cs
// MediatR 领域事件处理器 → 发布 MassTransit 消息
public class ProcessSubmittedEventHandler : INotificationHandler<ProcessSubmittedEvent>
{
    private readonly INotificationPublisher _publisher;
    private readonly IUnitOfWork _uow;

    public async Task Handle(ProcessSubmittedEvent evt, CancellationToken ct)
    {
        // 为每个新生成的任务通知对应审批人
        foreach (var task in evt.NewTasks)
        {
            var instance = await _uow.ProcessInstances.GetByIdAsync(evt.InstanceId, ct)!;
            var definition = await _uow.ProcessDefinitions.GetByIdAsync(instance.DefinitionId, ct)!;

            await _publisher.PublishAsync(new WorkflowNotification
            {
                InstanceId  = evt.InstanceId,
                EventType   = "task_assigned",
                RecipientId = task.AssigneeId,
                ProcessName = definition.Name,
                BusinessKey = instance.BusinessKey,
                IsUrgent    = task.IsUrgent,
                ExtraData   = new() { ["isDelegated"] = task.IsDelegated }
            }, ct);
        }
    }
}

// Application/EventHandlers/ProcessRejectedEventHandler.cs
public class ProcessRejectedEventHandler : INotificationHandler<ProcessRejectedEvent>
{
    public async Task Handle(ProcessRejectedEvent evt, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetByIdAsync(evt.InstanceId, ct)!;
        var initiator = instance.OnBehalfOf ?? instance.SubmittedBy;
        var definition = await _uow.ProcessDefinitions.GetByIdAsync(instance.DefinitionId, ct)!;

        await _publisher.PublishAsync(new WorkflowNotification
        {
            InstanceId  = evt.InstanceId,
            EventType   = "process_rejected",
            RecipientId = initiator,
            ProcessName = definition.Name,
            BusinessKey = instance.BusinessKey,
            Message     = evt.RejectReason,
        }, ct);
    }
}
```

### 13.3 MassTransit Consumer（RabbitMQ 消费者）

```csharp
// Infrastructure/Messaging/Consumers/NotificationConsumer.cs
public class NotificationConsumer : IConsumer<WorkflowNotification>
{
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly IInboxService _inbox;  // 站内信
    private readonly ILogger<NotificationConsumer> _logger;

    public async Task Consume(ConsumeContext<WorkflowNotification> context)
    {
        var msg = context.Message;
        _logger.LogInformation("处理通知：{EventType} -> {RecipientId}", msg.EventType, msg.RecipientId);

        // 站内信（所有场景）
        await _inbox.SendAsync(msg.RecipientId, BuildInboxMessage(msg));

        // 邮件（默认）
        await _email.SendAsync(msg.RecipientId, BuildEmailSubject(msg), BuildEmailBody(msg));

        // 短信（仅加急）
        if (msg.IsUrgent)
            await _sms.SendAsync(msg.RecipientId, $"【加急】{msg.ProcessName} 待您审批，请尽快处理");
    }
}

// MassTransit 配置（支持重试与死信队列）
services.AddMassTransit(x =>
{
    x.AddConsumer<NotificationConsumer>(cfg =>
    {
        cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)));
    });
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(config.GetConnectionString("RabbitMq"));
        cfg.ReceiveEndpoint("workflow-notifications", e =>
        {
            e.ConfigureConsumer<NotificationConsumer>(ctx);
            e.DeadLetterExchange = "workflow-notifications-dead";  // 死信队列
        });
    });
});
```

---

## 十四、并发控制

### 14.1 会签并发：Redis 分布式锁

多人会签时，多人可能同时提交，需防止重复推进流程。

```csharp
// Application/Services/IDistributedLockService.cs
public interface IDistributedLockService
{
    Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}

// Infrastructure/Caching/RedisDistributedLockService.cs
public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;

    public async Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var lockKey   = $"workflow:lock:{key}";
        var lockValue = Guid.NewGuid().ToString("N");

        var acquired = await db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);
        if (!acquired) return null;

        return new RedisLock(db, lockKey, lockValue);
    }

    private class RedisLock : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly string    _key;
        private readonly string    _value;

        public RedisLock(IDatabase db, string key, string value)
        {
            _db    = db;
            _key   = key;
            _value = value;
        }

        public async ValueTask DisposeAsync()
        {
            // Lua 脚本保证原子性删除（只删除自己的锁）
            const string script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";
            await _db.ScriptEvaluateAsync(script, new RedisKey[] { _key }, new RedisValue[] { _value });
        }
    }
}

// 在 ApproveTaskHandler 中使用分布式锁
public async Task Handle(ApproveTaskCommand cmd, CancellationToken ct)
{
    // 获取分布式锁，防止会签并发推进
    await using var stepLock = await _lockService.AcquireAsync(
        $"step:{task.StepId}:advance", TimeSpan.FromSeconds(10), ct);

    if (stepLock == null)
        throw new ConcurrentConflictException("操作冲突，请稍后重试");

    // ... 正常审批逻辑
}
```

### 14.2 数据库乐观锁（备选）

对于不需要 Redis 的轻量部署，使用 PostgreSQL 乐观锁：

```csharp
// Task 实体上的 RowVersion 字段（对应数据库 row_version INT）
public class WorkflowTask
{
    // ...
    [ConcurrencyCheck]
    public int RowVersion { get; set; }
}

// 或使用 EF Core RowVersion（byte[]）：
// builder.Property(x => x.RowVersion).IsRowVersion();

// 提交时若版本不匹配（其他人已修改），EF Core 抛出 DbUpdateConcurrencyException
// 在 Handler 中捕获并转换为业务错误
try
{
    await _uow.CommitAsync(ct);
}
catch (DbUpdateConcurrencyException)
{
    throw new ConcurrentConflictException("数据已被其他操作修改，请刷新后重试");
}
```

---

## 十五、测试策略

### 15.1 单元测试（In-Memory DB）

```csharp
// Tests/Unit/Application/ApproveTaskHandlerTests.cs
public class ApproveTaskHandlerTests
{
    private WorkflowDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())  // 每个测试独立 DB
            .Options;
        return new WorkflowDbContext(options);
    }

    [Fact]
    public async Task Approve_LastTask_ShouldCompleteProcess()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        var uow = BuildUnitOfWork(ctx);
        var (instance, step, task) = await SeedSingleStepProcess(uow);
        var handler = new ApproveTaskHandler(uow, Mock.Of<IMediator>(), Mock.Of<IDistributedLockService>());

        // Act
        await handler.Handle(new ApproveTaskCommand(task.Id, "uid_001", "同意"), CancellationToken.None);

        // Assert
        var updatedInstance = await ctx.ProcessInstances.FindAsync(instance.Id);
        Assert.Equal(ProcessStatus.Completed, updatedInstance!.Status);
    }

    [Fact]
    public async Task JointSign_AllPass_ShouldOnlyAdvanceWhenAllComplete()
    {
        // Arrange: 会签步骤，ALL_PASS 策略，2 个审批人
        await using var ctx = CreateInMemoryContext();
        var uow = BuildUnitOfWork(ctx);
        var (instance, step, tasks) = await SeedJointSignProcess(uow, JointSignPolicy.AllPass, 2);
        var handler = new ApproveTaskHandler(uow, Mock.Of<IMediator>(), Mock.Of<IDistributedLockService>());

        // Act: 第一个人通过
        await handler.Handle(new ApproveTaskCommand(tasks[0].Id, "uid_001", ""), CancellationToken.None);

        // Assert: 流程还在运行（等第二个人）
        var updatedInstance = await ctx.ProcessInstances.FindAsync(instance.Id);
        Assert.Equal(ProcessStatus.Running, updatedInstance!.Status);

        // Act: 第二个人通过
        await handler.Handle(new ApproveTaskCommand(tasks[1].Id, "uid_002", ""), CancellationToken.None);

        // Assert: 流程完成
        updatedInstance = await ctx.ProcessInstances.FindAsync(instance.Id);
        Assert.Equal(ProcessStatus.Completed, updatedInstance!.Status);
    }
}
```

### 15.2 集成测试（TestContainers）

```csharp
// Tests/Integration/API/SubmitProcessTests.cs
// 使用 Testcontainers.PostgreSql 启动真实 PostgreSQL 容器
public class SubmitProcessTests : IAsyncLifetime
{
    private PostgreSqlContainer _pgContainer = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _pgContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .Build();
        await _pgContainer.StartAsync();

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // 替换 PostgreSQL 为测试容器连接
                    services.RemoveAll<DbContextOptions<WorkflowDbContext>>();
                    services.AddDbContext<WorkflowDbContext>(opt =>
                        opt.UseNpgsql(_pgContainer.GetConnectionString()));
                });
            });
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submit_WithProxyPermission_ShouldCreateInstance()
    {
        // ... 完整的 HTTP 调用集成测试
    }

    public async Task DisposeAsync() => await _pgContainer.DisposeAsync();
}
```

### 15.3 扩展点 Mock

```csharp
// 测试时 mock 扩展点，不依赖真实外部服务
public class FakeApproverResolver : IApproverResolver
{
    public Task<ResolveApproversResult> ResolveAsync(ResolveApproversContext ctx, CancellationToken ct)
    {
        return Task.FromResult(new ResolveApproversResult(
            Steps: [
                new ResolvedStep(1, StepType.Approval,
                    [new ResolvedAssignee("uid_approver_01")], null)
            ],
            Metadata: null));
    }
}

public class FakePermissionValidator : IPermissionValidator
{
    public bool ShouldPass { get; set; } = true;

    public Task<ValidatePermissionsResult> ValidateAsync(ValidatePermissionsContext ctx, CancellationToken ct)
    {
        return Task.FromResult(ShouldPass
            ? new ValidatePermissionsResult(true, [], "")
            : new ValidatePermissionsResult(false,
                [new PermissionFailItem(1, "uid_001", "权限不足")], "校验失败"));
    }
}

// 在测试 DI 中注册 fake 实现
services.AddScoped<IApproverResolver, FakeApproverResolver>();
services.AddScoped<IPermissionValidator, FakePermissionValidator>();
```

---

## 附录：关键配置文件

### appsettings.json

```json
{
  "ConnectionStrings": {
    "WorkflowDb": "Host=localhost;Port=5432;Database=workflow_db;Username=workflow_user;Password=****",
    "Redis":      "localhost:6379,password=****",
    "RabbitMq":   "amqp://guest:guest@localhost:5672"
  },
  "Workflow": {
    "ExtensionPoint": {
      "TimeoutSeconds": 5,
      "RetryCount": 2,
      "FallbackOnTimeout": true
    },
    "StepIndex": {
      "MinPrecision": 4,           // stepIndex 小数位数，决定最大插入层数（10^4 = 10000 次）
      "RebalanceThreshold": 0.001  // 低于此间距时触发重新整理 stepIndex
    }
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/workflow-.log", "rollingInterval": "Day" } }
    ]
  }
}
```

### Program.cs 主入口（简略）

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services
    .AddControllers()
    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssembly(typeof(IApplicationMarker).Assembly));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IApplicationMarker).Assembly));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Infrastructure DI（生产：PostgreSQL；测试：传 useInMemory=true）
builder.Services.AddWorkflowInfrastructure(builder.Configuration, useInMemory: false);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.MapControllers();

// 自动应用 EF 迁移（生产建议改为手动执行）
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
    await ctx.Database.MigrateAsync();
}

await app.RunAsync();
```

---

*本文档与 workflow-engine-design.md 配套使用。如需进一步展开特定模块（如规则引擎 DSL、消息通知渠道扩展、多租户隔离方案），可在此基础上补充。*
