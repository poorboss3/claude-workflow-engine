-- =============================================================
-- WorkflowEngine 初始数据库迁移脚本
-- 适用于 PostgreSQL 14+
-- 执行前确保数据库和用户已创建：
--   CREATE DATABASE workflow_db;
--   CREATE USER workflow_user WITH PASSWORD 'workflow_pass';
--   GRANT ALL PRIVILEGES ON DATABASE workflow_db TO workflow_user;
-- =============================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================
-- 流程定义
-- ============================================================
CREATE TABLE IF NOT EXISTS process_definitions (
    id                       UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    name                     VARCHAR(200) NOT NULL,
    process_type             VARCHAR(100) NOT NULL,
    version                  INT          NOT NULL DEFAULT 1,
    status                   VARCHAR(20)  NOT NULL DEFAULT 'Draft',
    node_templates           JSONB        NOT NULL DEFAULT '[]',
    rule_set_id              UUID,
    approver_resolver_url    VARCHAR(500),
    permission_validator_url VARCHAR(500),
    created_by               VARCHAR(100) NOT NULL,
    created_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_process_type_version UNIQUE (process_type, version)
);

CREATE INDEX IF NOT EXISTS idx_pd_process_type ON process_definitions (process_type);
CREATE INDEX IF NOT EXISTS idx_pd_status        ON process_definitions (status);

-- ============================================================
-- 流程实例
-- ============================================================
CREATE TABLE IF NOT EXISTS process_instances (
    id                   UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    definition_id        UUID         NOT NULL REFERENCES process_definitions(id),
    definition_version   INT          NOT NULL,
    business_key         VARCHAR(200) NOT NULL,
    form_data_snapshot   JSONB        NOT NULL DEFAULT '{}',
    submitted_by         VARCHAR(100) NOT NULL,
    on_behalf_of         VARCHAR(100),
    status               VARCHAR(30)  NOT NULL DEFAULT 'Running',
    is_urgent            BOOLEAN      NOT NULL DEFAULT FALSE,
    current_step_index   DECIMAL(10,4),
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    completed_at         TIMESTAMPTZ,
    CONSTRAINT uq_business_key UNIQUE (business_key)
);

CREATE INDEX IF NOT EXISTS idx_pi_status       ON process_instances (status);
CREATE INDEX IF NOT EXISTS idx_pi_submitted_by ON process_instances (submitted_by);
CREATE INDEX IF NOT EXISTS idx_pi_on_behalf_of ON process_instances (on_behalf_of);
CREATE INDEX IF NOT EXISTS idx_pi_created_at   ON process_instances (created_at DESC);

-- ============================================================
-- 审批步骤
-- ============================================================
CREATE TABLE IF NOT EXISTS approval_steps (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    instance_id       UUID         NOT NULL REFERENCES process_instances(id) ON DELETE CASCADE,
    step_index        DECIMAL(10,4) NOT NULL,
    type              VARCHAR(30)  NOT NULL,
    assignees         JSONB        NOT NULL DEFAULT '[]',
    joint_sign_policy VARCHAR(20),
    status            VARCHAR(30)  NOT NULL DEFAULT 'Pending',
    source            VARCHAR(30)  NOT NULL DEFAULT 'Original',
    added_by_user_id  VARCHAR(100),
    added_at          TIMESTAMPTZ,
    completed_at      TIMESTAMPTZ,
    CONSTRAINT uq_step_index UNIQUE (instance_id, step_index)
);

CREATE INDEX IF NOT EXISTS idx_as_instance_id ON approval_steps (instance_id);
CREATE INDEX IF NOT EXISTS idx_as_status      ON approval_steps (instance_id, status);
CREATE INDEX IF NOT EXISTS idx_as_step_index  ON approval_steps (instance_id, step_index);

-- ============================================================
-- 任务
-- ============================================================
CREATE TABLE IF NOT EXISTS tasks (
    id                    UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    instance_id           UUID         NOT NULL REFERENCES process_instances(id) ON DELETE CASCADE,
    step_id               UUID         NOT NULL REFERENCES approval_steps(id) ON DELETE CASCADE,
    assignee_id           VARCHAR(100) NOT NULL,
    original_assignee_id  VARCHAR(100),
    is_delegated          BOOLEAN      NOT NULL DEFAULT FALSE,
    status                VARCHAR(30)  NOT NULL DEFAULT 'Pending',
    is_urgent             BOOLEAN      NOT NULL DEFAULT FALSE,
    action                VARCHAR(30),
    comment               TEXT,
    row_version           INT          NOT NULL DEFAULT 0,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    completed_at          TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_task_assignee_pending
    ON tasks (assignee_id, status) WHERE status = 'Pending';
CREATE INDEX IF NOT EXISTS idx_task_assignee_completed
    ON tasks (assignee_id, completed_at DESC) WHERE status IN ('Completed','Returned','Rejected');
CREATE INDEX IF NOT EXISTS idx_task_original_assignee
    ON tasks (original_assignee_id) WHERE is_delegated = TRUE;
CREATE INDEX IF NOT EXISTS idx_task_instance_id ON tasks (instance_id);
CREATE INDEX IF NOT EXISTS idx_task_step_id     ON tasks (step_id);

-- ============================================================
-- 审批规则
-- ============================================================
CREATE TABLE IF NOT EXISTS approval_rules (
    id                    UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    name                  VARCHAR(200) NOT NULL,
    priority              INT          NOT NULL DEFAULT 0,
    process_definition_id UUID         REFERENCES process_definitions(id),
    conditions            JSONB        NOT NULL DEFAULT '[]',
    condition_logic       VARCHAR(10)  NOT NULL DEFAULT 'AND',
    result                JSONB        NOT NULL DEFAULT '{}',
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_ar_process_def ON approval_rules (process_definition_id, is_active, priority DESC);
CREATE INDEX IF NOT EXISTS idx_ar_global      ON approval_rules (is_active, priority DESC)
    WHERE process_definition_id IS NULL;

-- ============================================================
-- 审批列表修改记录
-- ============================================================
CREATE TABLE IF NOT EXISTS approver_list_modifications (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    instance_id     UUID         NOT NULL REFERENCES process_instances(id) ON DELETE CASCADE,
    modified_by     VARCHAR(100) NOT NULL,
    modified_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    original_steps  JSONB        NOT NULL DEFAULT '[]',
    final_steps     JSONB        NOT NULL DEFAULT '[]',
    diff_summary    JSONB        NOT NULL DEFAULT '[]'
);

CREATE INDEX IF NOT EXISTS idx_alm_instance_id ON approver_list_modifications (instance_id, modified_at DESC);

-- ============================================================
-- 代理配置
-- ============================================================
CREATE TABLE IF NOT EXISTS proxy_configs (
    id                    UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    principal_id          VARCHAR(100) NOT NULL,
    agent_id              VARCHAR(100) NOT NULL,
    allowed_process_types JSONB        NOT NULL DEFAULT '[]',
    valid_from            TIMESTAMPTZ  NOT NULL,
    valid_to              TIMESTAMPTZ  NOT NULL,
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_pc_agent_id    ON proxy_configs (agent_id, is_active);
CREATE INDEX IF NOT EXISTS idx_pc_principal   ON proxy_configs (principal_id, is_active);
CREATE INDEX IF NOT EXISTS idx_pc_active_range ON proxy_configs (agent_id, valid_from, valid_to)
    WHERE is_active = TRUE;

-- ============================================================
-- 委托配置
-- ============================================================
CREATE TABLE IF NOT EXISTS delegation_configs (
    id                    UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    delegator_id          VARCHAR(100) NOT NULL,
    delegatee_id          VARCHAR(100) NOT NULL,
    allowed_process_types JSONB        NOT NULL DEFAULT '[]',
    valid_from            TIMESTAMPTZ  NOT NULL,
    valid_to              TIMESTAMPTZ  NOT NULL,
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    reason                VARCHAR(500),
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_dc_delegator   ON delegation_configs (delegator_id, is_active);
CREATE INDEX IF NOT EXISTS idx_dc_active_range ON delegation_configs (delegator_id, valid_from, valid_to)
    WHERE is_active = TRUE;

-- ============================================================
-- 初始化示例数据（可选）
-- ============================================================
-- INSERT INTO process_definitions (name, process_type, status, created_by)
-- VALUES ('费用报销', 'expense_report', 'Active', 'admin');
