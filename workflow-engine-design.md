# BPM 工作流引擎系统设计方案

> 版本：1.0 | 日期：2026-03-13

---

## 目录

1. [核心设计理念](#一核心设计理念)
2. [系统模块划分](#二系统模块划分)
3. [核心数据模型](#三核心数据模型)
4. [提交流程详细设计](#四提交流程详细设计)
5. [核心审批操作](#五核心审批操作)
6. [动态审批人管理](#六动态审批人管理)
7. [代理与委托管理](#七代理与委托管理)
8. [任务列表设计](#八任务列表设计)
9. [扩展点接口设计](#九扩展点接口设计解耦)
10. [通知服务设计](#十通知服务设计)
11. [技术选型建议](#十一技术选型建议)
12. [设计优势总结](#十二设计优势总结)

---

## 一、核心设计理念

### 动态步骤链（Dynamic Step Chain）

采用**有序步骤列表**替代复杂的 DAG（有向无环图）。所有审批步骤以有序列表形式存储在流程实例中，支持运行时插入、删除、调整顺序。

**优势：**
- 天然支持动态修改审批人、加签等操作
- 维护成本远低于 DAG
- 状态清晰，易于查询和审计

### 扩展点解耦

流程引擎通过**扩展点接口**与业务系统交互，引擎不感知任何业务逻辑，实现流程引擎与业务系统的完全解耦。

---

## 二、系统模块划分

```
┌─────────────────────────────────────────────────────────┐
│                     API 层（REST）                        │
├──────────┬──────────┬──────────┬──────────┬─────────────┤
│ 流程管理  │ 任务管理  │ 规则引擎  │ 代理管理  │  委托管理    │
├──────────┴──────────┴──────────┴──────────┴─────────────┤
│                    领域服务层                              │
│  WorkflowService / TaskService / NotificationService     │
├─────────────────────────────────────────────────────────┤
│              基础设施层（DB / MQ / Cache）                 │
└─────────────────────────────────────────────────────────┘
```

| 模块 | 职责 |
|------|------|
| 流程定义管理 | 模板配置、节点模板、版本管理 |
| 流程实例管理 | 运行时状态、审批步骤链管理 |
| 任务管理 | 待办/已办列表、任务操作 |
| 审批规则引擎 | 根据表单数据计算默认审批人列表 |
| 代理管理 | 代提交权限配置与校验 |
| 委托管理 | 审批委托配置，透明替换审批人 |
| 通知服务 | 消息解耦，异步推送 |

---

## 三、核心数据模型

### 3.1 流程定义（ProcessDefinition）

```
ProcessDefinition
├── id
├── name
├── version
├── status                 // draft | active | archived
├── nodeTemplates[]        // 节点模板，定义默认审批结构
├── ruleSetId              // 关联的审批规则集
└── extensionPoints        // 扩展点配置
    ├── approverResolverUrl
    └── permissionValidatorUrl
```

### 3.2 流程实例（ProcessInstance）

```
ProcessInstance
├── id
├── definitionId
├── definitionVersion      // 快照版本，防止定义变更影响运行中流程
├── businessKey            // 关联业务单据的唯一标识
├── formDataSnapshot       // 提交时表单数据快照（JSON）
├── submittedBy            // 实际操作提交的人
├── onBehalfOf             // 代提交时：被代理的原始发起人（可为空）
├── status                 // running | completed | rejected | withdrawn
├── isUrgent               // 是否加急
├── currentStepIndex       // 当前激活的审批步骤位置
└── approvalSteps[]        // 有序审批步骤列表（核心）
```

### 3.3 审批步骤（ApprovalStep）

步骤链的核心实体，支持动态插入和调整。

```
ApprovalStep
├── id
├── instanceId
├── stepIndex              // 决定顺序，插入时取相邻值中间数（如 1.5）
├── type                   // approval | joint_sign | notify
├── assignees[]            // 审批人列表（会签时多人）
├── jointSignPolicy        // 会签策略：ALL_PASS | MAJORITY | ANY_ONE
├── status                 // pending | active | completed | rejected | returned | skipped
├── source                 // original | countersign | dynamic_added
├── addedByUserId          // 加签时：谁发起的加签
├── addedAt
└── completedAt
```

**stepIndex 插入示例：**

```
原始：步骤1(index=1) → 步骤2(index=2) → 步骤3(index=3)
加签：步骤1(index=1) → 新步骤(index=1.5) → 步骤2(index=2) → 步骤3(index=3)
```

### 3.4 任务（Task）

面向人的执行单元，一个步骤可对应多个任务（会签场景）。

```
Task
├── id
├── instanceId
├── stepId
├── assigneeId             // 实际处理人（委托后为受托人）
├── originalAssigneeId     // 原始审批人（委托时保留，用于审计）
├── isDelegated            // 是否为委托任务
├── status                 // pending | completed | returned | rejected
├── isUrgent
├── action                 // approve | reject | return | delegate | countersign | notify_read
├── comment                // 审批意见
└── completedAt
```

### 3.5 审批规则（ApprovalRule）

```
ApprovalRule
├── id
├── name
├── priority               // 多条规则冲突时，取最高优先级
├── processDefinitionId    // 关联流程定义（空=全局规则）
├── conditions[]           // 条件配置（JSON）
├── conditionLogic         // AND | OR
└── result                 // 满足条件时返回的审批步骤配置（JSON）
```

**规则配置示例（JSON，运营人员通过后台界面维护）：**

```json
{
  "conditions": [
    { "field": "amount",     "operator": "gt", "value": 10000 },
    { "field": "department", "operator": "eq", "value": "finance" }
  ],
  "conditionLogic": "AND",
  "result": {
    "steps": [
      {
        "type": "approval",
        "approvers": [{ "type": "role", "value": "dept_manager" }]
      },
      {
        "type": "approval",
        "approvers": [{ "type": "position", "value": "CFO" }]
      },
      {
        "type": "joint_sign",
        "approvers": [
          { "type": "user", "value": "uid_001" },
          { "type": "user", "value": "uid_002" }
        ],
        "policy": "ALL_PASS"
      }
    ]
  }
}
```

**条件操作符支持：** `eq | neq | gt | gte | lt | lte | in | contains | regex`

**审批人来源类型：** `user（指定人）| role（角色）| position（职位）| department_head（部门负责人）| direct_supervisor（直属上级）`

### 3.6 审批列表修改记录（ApproverListModification）

```
ApproverListModification
├── id
├── instanceId
├── modifiedBy
├── modifiedAt
├── originalSteps[]        // 规则引擎返回的原始快照（JSON）
├── finalSteps[]           // 提交时用户确认的最终列表（JSON）
└── diffSummary[]          // 结构化 diff
    ├── { action: "added",    stepIndex: 2, assigneeId: "uid_003" }
    ├── { action: "removed",  stepIndex: 3, assigneeId: "uid_007" }
    └── { action: "replaced", stepIndex: 1, from: "uid_001", to: "uid_002" }
```

> 无修改时 `diffSummary` 为空数组，但快照仍然保存，用于审计溯源。

### 3.7 代理配置（ProxyConfig）

```
ProxyConfig
├── id
├── principalId            // 被代理人（B）
├── agentId                // 代理人（A）
├── allowedProcessTypes[]  // 可代理的流程类型，空=全部
├── validFrom
├── validTo
└── isActive
```

### 3.8 委托配置（DelegationConfig）

```
DelegationConfig
├── id
├── delegatorId            // 委托人（如：休假者）
├── delegateeId            // 受托人
├── allowedProcessTypes[]  // 委托范围，空=全部
├── validFrom
├── validTo
├── isActive
└── reason                 // 委托原因（休假/出差等）
```

---

## 四、提交流程详细设计

### 4.1 完整提交时序

```
用户打开表单
      │
      ▼
① [引擎调用扩展点] ApproverResolver.resolve(context)
      │    └── 业务系统根据表单内容返回默认审批步骤列表
      │
      ▼
② 前端展示默认审批列表，用户可调整（增删改顺序）
      │
      ▼
③ 用户点击"提交"
      │
      ▼
④ [代提交校验]
      │    └── 若 submittedBy ≠ onBehalfOf，查 ProxyConfig 验证权限
      │    └── 无权限 → 返回错误，终止
      │
      ▼
⑤ [引擎内部] 对比原始列表与用户确认列表，生成 diff，记录修改快照
      │
      ▼
⑥ [引擎调用扩展点] PermissionValidator.validate(context)
      │    └── 业务系统验证最终审批列表中每人的审批权限
      │    └── 验证失败 → 返回结构化错误，终止提交，提示用户
      │
      ▼
⑦ [委托透明替换]
      │    └── 对每个步骤的 assignee 查询 DelegationConfig
      │    └── 若有有效委托 → originalAssigneeId=原始人，assigneeId=受托人，isDelegated=true
      │
      ▼
⑧ 创建 ProcessInstance + ApprovalSteps + ApproverListModification
      │
      ▼
⑨ 激活第一个步骤，为对应 assignee 生成 Task
      │
      ▼
⑩ 发送通知（异步）
```

### 4.2 扩展点与引擎职责边界

| 引擎负责 | 业务系统负责 |
|---------|------------|
| 调用扩展点的时机与顺序 | 审批人列表的计算逻辑 |
| 记录修改 diff 和快照 | 权限规则的判断逻辑 |
| 验证失败时终止流程 | 返回结构化的验证结果 |
| 存储修改记录 | 维护审批权限数据 |
| 接口契约的版本管理 | 接口的具体实现 |

> 流程引擎对业务逻辑**零感知**——只知道"调谁、传什么、收什么"，不知道"为什么这些人有权限"。

---

## 五、核心审批操作

### 5.1 操作状态机

```
              通过(approve)
    ┌────────────────────────────► 下一步激活 / 流程完成
    │
待处理(pending) ──退回(return)──► 目标步骤重新激活（退回上一步 or 退回发起人）
    │
    └──────驳回(reject)─────────► 整个流程终止，通知发起人
```

### 5.2 通过（Approve）

1. 标记当前 Task 为 `completed`，记录操作意见
2. 检查当前 Step 是否满足推进条件（普通审批：当前人完成即可；会签：按 jointSignPolicy 判断）
3. 推进到下一个 `pending` 步骤，激活并生成对应 Task
4. 若无下一步骤 → 流程完成，更新实例状态为 `completed`，通知发起人

### 5.3 退回（Return）

- **退回上一步**：将上一个 `completed` 步骤状态重置为 `active`，重新生成 Task
- **退回发起人**：将流程退回到初始状态，发起人可重新修改并提交
- 退回时必须填写退回原因

### 5.4 驳回（Reject）

- 流程直接终止，所有 `pending/active` 步骤标记为 `skipped`
- 实例状态更新为 `rejected`
- 异步通知发起人，附带驳回原因

### 5.5 加签（Countersign）

当前审批人在自己步骤**之后**插入一个新步骤：

1. 新建 ApprovalStep，`stepIndex` 取当前步骤与下一步骤的中间值，`source=countersign`
2. 将新步骤加入步骤链
3. 当前步骤仍需完成正常审批动作后方可流转到加签步骤

### 5.6 会签（Joint Sign）

同一个 ApprovalStep 中 `assignees` 为多人，每人各生成一个独立 Task：

| 策略 | 说明 |
|------|------|
| `ALL_PASS` | 所有人通过才推进到下一步 |
| `ANY_ONE` | 任意一人通过即推进 |
| `MAJORITY` | 超半数通过即推进 |

任意一人驳回时：默认整个流程驳回（可按流程定义配置是否继续）。

### 5.7 通知（Notify）

`type=notify` 的步骤不需要审批动作，仅通知相关人知悉：

- 系统自动生成通知，assignee 标记"已读"即完成
- 通知步骤不阻塞流程推进（异步通知，流程直接流转到下一步）

### 5.8 加急（Urgent）

- 标记 `ProcessInstance.isUrgent = true` 及对应 `Task.isUrgent = true`
- 通知服务使用高优先级渠道（短信/电话等）
- 任务列表中置顶显示
- 可配置 SLA 超时告警时间

---

## 六、动态审批人管理

### 6.1 可操作范围

| 操作 | 前提条件 | 操作人 |
|------|---------|--------|
| 替换某步骤审批人 | 步骤状态为 `pending`（未激活） | 流程发起人 / 管理员 |
| 插入新步骤 | 插入位置在当前步骤之后 | 当前审批人 / 管理员 |
| 删除步骤 | 步骤状态为 `pending` | 流程发起人 / 管理员 |
| 调整步骤顺序 | 涉及步骤均为 `pending` | 流程发起人 / 管理员 |

### 6.2 修改记录

每次修改均追加记录到 `ApproverListModification`，保留完整操作历史，支持审计查看。

---

## 七、代理与委托管理

### 7.1 代提交（Proxy Submission）

**场景**：A 代 B 提交申请单。

- 提交时传入 `submittedBy=A, onBehalfOf=B`
- 引擎校验 ProxyConfig 中是否存在 A 可代理 B 的有效配置
- 流程实例记录两个字段，审批历史中清晰显示"A 代 B 提交"
- 代理人列表通过查询 `ProxyConfig WHERE agentId=currentUser AND isActive=true` 获取

### 7.2 委托审批（Delegation）

**场景**：审批人 C 休假，委托 D 代为处理审批任务。

- C 在休假前配置 DelegationConfig（指定时间范围、委托范围）
- 提交阶段（步骤七）：引擎对每个 assignee 透明检查委托配置
- 若 C 有有效委托，Task 的 `assigneeId=D, originalAssigneeId=C, isDelegated=true`
- D 处理任务时，审批历史显示"D 代 C 审批"
- 委托到期后自动失效，新任务恢复分配给 C

**透明性设计**：委托对流程本身透明，流程定义无需感知委托关系，始终保留原始审批人记录。

---

## 八、任务列表设计

### 8.1 待办任务（My Pending Tasks）

**查询：**
```
Task WHERE assigneeId = currentUser
         AND status = 'pending'
ORDER BY isUrgent DESC, createdAt ASC
```

**展示字段：**

| 字段 | 说明 |
|------|------|
| 流程名称 | 如"费用报销申请" |
| 发起人 | onBehalfOf（若存在）否则 submittedBy |
| 提交时间 | 实例创建时间 |
| 待处理时长 | 当前时间 - task.createdAt |
| 是否加急 | 置顶 + 红色标识 |
| 是否委托 | "代 XX 审批" 标注 |
| 表单摘要 | 从 formDataSnapshot 提取关键字段 |

### 8.2 已办任务（My Completed Tasks）

**查询：**
```
Task WHERE (assigneeId = currentUser OR originalAssigneeId = currentUser)
         AND status IN ('completed', 'returned', 'rejected')
ORDER BY completedAt DESC
```

通过 `isDelegated` 标记区分自己处理和代委托人处理的任务。

---

## 九、扩展点接口设计（解耦）

流程引擎定义接口契约，业务系统自行实现，实现方式支持以下两种：

### 9.1 方案 A：HTTP Callback（推荐，跨服务部署）

在 ProcessDefinition 中配置回调地址：

```json
{
  "extensionPoints": {
    "approverResolverUrl": "https://biz-service/workflow/resolve-approvers",
    "permissionValidatorUrl": "https://biz-service/workflow/validate-permissions"
  }
}
```

**ApproverResolver 接口契约：**

```
POST /workflow/resolve-approvers

Request：
{
  "processType": "expense_report",
  "formData": { "amount": 15000, "department": "finance", ... },
  "submittedBy": "uid_001",
  "onBehalfOf": "uid_002"       // 可为空
}

Response：
{
  "steps": [
    {
      "type": "approval",
      "assignees": [{ "type": "role", "value": "dept_manager" }]
    },
    {
      "type": "joint_sign",
      "assignees": [
        { "type": "user", "value": "uid_010" },
        { "type": "user", "value": "uid_011" }
      ],
      "policy": "ALL_PASS"
    }
  ],
  "metadata": {}               // 业务系统透传上下文，引擎不解析
}
```

**PermissionValidator 接口契约：**

```
POST /workflow/validate-permissions

Request：
{
  "processType": "expense_report",
  "formData": { "amount": 15000, ... },
  "submittedBy": "uid_001",
  "originalSteps": [...],      // 规则引擎返回的原始列表
  "finalSteps": [...],         // 用户确认的最终列表
  "isModified": true
}

Response（通过）：
{
  "passed": true,
  "failedItems": [],
  "message": ""
}

Response（失败）：
{
  "passed": false,
  "failedItems": [
    {
      "stepIndex": 1,
      "assigneeId": "uid_010",
      "reason": "该用户审批额度上限为 10000，当前申请金额超出限制"
    }
  ],
  "message": "审批人权限不足，请调整审批列表"
}
```

**注意事项：**
- 配置合理超时时间（建议 3s），超时按降级策略处理
- 建议增加 `requestId` 字段用于日志追踪
- 接口版本通过 Header 传递（如 `X-Workflow-Version: 1`）

### 9.2 方案 B：SPI 插件（同进程部署）

适合流程引擎作为 SDK 嵌入业务系统的场景：

```java
// 引擎定义接口
interface ApproverResolver {
    ResolveResult resolve(ResolveContext ctx);
}

interface PermissionValidator {
    ValidationResult validate(ValidationContext ctx);
}

// 业务系统实现并注入
@Component
public class BizApproverResolver implements ApproverResolver {
    @Override
    public ResolveResult resolve(ResolveContext ctx) {
        // 业务逻辑
    }
}
```

---

## 十、通知服务设计

通知服务通过消息队列与核心流程解耦，异步处理所有通知场景。

### 10.1 通知触发时机

| 事件 | 通知对象 | 说明 |
|------|---------|------|
| 流程提交 | 第一个审批人 | 新任务待处理 |
| 审批通过/步骤流转 | 下一步审批人 | 新任务待处理 |
| 审批驳回 | 流程发起人 | 流程被驳回 |
| 退回 | 退回目标人 | 需要重新处理 |
| 加急标记 | 当前待处理人 | 高优先级通知 |
| 流程完成 | 流程发起人 | 审批完成 |
| 委托任务 | 受托人 | 代理新任务 |

### 10.2 通知渠道

- 系统内消息（站内信）
- 邮件
- 短信（加急场景）
- 企业 IM（企业微信/钉钉等，按集成情况选择）

---

## 十一、技术选型建议

| 层次 | 推荐 | 理由 |
|------|------|------|
| 后端语言 | Java（Spring Boot）或 Go | Java 生态成熟，适合企业级；Go 性能高、部署轻量 |
| 数据库 | PostgreSQL | 关系型保证事务一致性，JSONB 类型支持灵活存储表单数据和规则配置 |
| 缓存 | Redis | 任务列表缓存、分布式锁防并发冲突（如会签并发提交） |
| 消息队列 | RabbitMQ | 通知异步解耦，流量削峰，使用简单 |
| 规则配置 | DB + 运营后台 | 规则以 JSON 存储于数据库，提供低代码配置界面，变更实时生效 |

### 关键并发处理

会签场景下多人同时提交，需通过 Redis 分布式锁或数据库乐观锁防止重复推进：

```
LOCK key="step:{stepId}:complete"
  → 检查步骤当前完成人数
  → 判断是否满足推进条件
  → 满足则推进，不满足则释放锁
UNLOCK
```

---

## 十二、设计优势总结

| 需求点 | 设计应对 |
|--------|---------|
| 提交/退回/驳回/通过 | 操作状态机，步骤链驱动流转 |
| 加签/会签 | ApprovalStep.type + assignees[] + jointSignPolicy 统一建模 |
| 通知/加急 | isUrgent 标记 + 异步通知服务 |
| 动态修改审批人 | stepIndex 浮点插入，支持运行时增删改排 |
| 灵活审批规则 | JSON 条件配置，优先级匹配，运营后台维护，零代码变更 |
| 代提交 | ProxyConfig 独立模块，提交入口统一校验 |
| 委托审批 | DelegationConfig + 创建任务时透明替换，保留原始信息 |
| 待办/已办列表 | Task 表独立查询，assigneeId + originalAssigneeId 双索引 |
| 审批权限验证 | PermissionValidator 扩展点，与引擎完全解耦 |
| 审批人获取解耦 | ApproverResolver 扩展点，业务系统自行实现 |
| 审计追溯 | originalAssigneeId + isDelegated + 修改记录快照 |

---

*文档持续迭代，如需进一步展开 API 接口规范、数据库索引设计、或具体模块实现方案，请在此文档基础上补充。*
