# WorkflowEngine

基于 ASP.NET Core 9 的 BPM 工作流引擎，实现提交、审批、退回、驳回、加签、会签、代理、委托等完整流程功能。

## 项目结构

```
WorkflowEngine/
├── src/
│   ├── WorkflowEngine.Domain/          # 领域层：实体、枚举、仓储接口、领域事件
│   ├── WorkflowEngine.Application/     # 应用层：命令/查询 Handler、扩展点接口
│   ├── WorkflowEngine.Infrastructure/  # 基础设施：EF Core、Redis、RabbitMQ
│   └── WorkflowEngine.API/             # API 层：Controllers、Middleware
└── tests/
    └── WorkflowEngine.Tests/           # 单元测试（In-Memory DB）
```

## 快速启动

### 前置依赖（生产模式）
- PostgreSQL 14+ 或 MySQL 8.0+
- Redis 7+
- RabbitMQ 3.12+

### 开发模式（无外部依赖）
`appsettings.Development.json` 默认 `DatabaseProvider: "InMemory"`，使用 In-Memory 数据库和内存消息总线，**无需任何外部服务**。

```bash
cd src/WorkflowEngine.API
dotnet run
# Swagger UI: http://localhost:5000/swagger
```

### 生产部署

1. 修改 `appsettings.json`，设置数据库提供程序及连接字符串：

**使用 PostgreSQL：**
```json
"UseInMemoryDb": false,
"DatabaseProvider": "PostgreSQL",
"ConnectionStrings": {
  "WorkflowDb": "Host=localhost;Port=5432;Database=workflow_db;Username=workflow_user;Password=workflow_pass"
}
```

**使用 MySQL：**
```json
"UseInMemoryDb": false,
"DatabaseProvider": "MySQL",
"ConnectionStrings": {
  "WorkflowDbMySql": "Server=localhost;Port=3306;Database=workflow_db;User=workflow_user;Password=workflow_pass;"
}
```

2. 执行 EF Core 数据库迁移
```bash
dotnet ef migrations add InitialCreate --project src/WorkflowEngine.Infrastructure --startup-project src/WorkflowEngine.API
dotnet ef database update --project src/WorkflowEngine.Infrastructure --startup-project src/WorkflowEngine.API
```

3. 启动服务
```bash
dotnet publish -c Release
dotnet WorkflowEngine.API.dll
```

## 运行测试

```bash
dotnet test tests/WorkflowEngine.Tests/
```

测试使用 In-Memory 数据库，无需任何外部服务。

## 核心 API

### 提交流程
```
POST /api/v1/process-instances/prepare   # 获取默认审批人列表
POST /api/v1/process-instances           # 正式提交
```

### 审批操作
```
POST /api/v1/tasks/{id}/approve     # 通过
POST /api/v1/tasks/{id}/reject      # 驳回
POST /api/v1/tasks/{id}/return      # 退回
POST /api/v1/tasks/{id}/countersign # 加签
```

### 任务列表
```
GET /api/v1/tasks/pending    # 我的待办
GET /api/v1/tasks/completed  # 我的已办
```

### 代理与委托
```
GET  /api/v1/proxy-configs/my-principals   # 我可代理的人
POST /api/v1/proxy-configs                 # 创建代理配置
POST /api/v1/delegation-configs            # 创建委托配置
```

## 当前用户识别

开发测试时通过 Header 传入用户 ID：
```
X-User-Id: uid_001
```

生产环境通过 JWT Claims（`ClaimTypes.NameIdentifier`）自动提取。

## 扩展点接入

在流程定义中配置回调 URL：
```json
{
  "approverResolverUrl": "https://your-biz-service/workflow/resolve-approvers",
  "permissionValidatorUrl": "https://your-biz-service/workflow/validate-permissions"
}
```

详细接口契约见 `workflow-engine-design.md` 第九章。
