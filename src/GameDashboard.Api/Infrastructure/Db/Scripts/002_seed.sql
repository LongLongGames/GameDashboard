INSERT INTO roles (name, description) VALUES
    ('SuperAdmin', '超级管理员：账号/角色/游戏接入配置'),
    ('GM', 'GM：查玩家、封禁、改货币、发邮件/补偿'),
    ('Operator', '运营：发奖、活动、CDK、基础统计'),
    ('Developer', '程序：查 BugReport、版本/资源、健康检查')
ON CONFLICT (name) DO NOTHING;

-- root / ChangeMe123!  (BCrypt hash generated for ChangeMe123!)
-- hash placeholder replaced at runtime by seed if missing; keep SQL idempotent for roles only
