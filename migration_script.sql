-- SmartWorkFlowX Incremental Migration Script
-- Applies ONLY the new columns and new tables to an existing database schema.
-- Run this in SQL Server Management Studio or via sqlcmd.

-- === STEP 1: Create EF migrations history table if not present ===
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

-- === STEP 2: Add Status column to Workflows (if not exists) ===
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Workflows') AND name = 'Status')
BEGIN
    ALTER TABLE [Workflows] ADD [Status] nvarchar(max) NOT NULL DEFAULT N'Draft';
    PRINT 'Added Status to Workflows';
END
GO

-- === STEP 3: Add new columns to WorkflowSteps ===
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkflowSteps') AND name = 'StepName')
BEGIN
    ALTER TABLE [WorkflowSteps] ADD [StepName] nvarchar(max) NOT NULL DEFAULT N'';
    PRINT 'Added StepName to WorkflowSteps';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkflowSteps') AND name = 'Description')
BEGIN
    ALTER TABLE [WorkflowSteps] ADD [Description] nvarchar(max) NULL;
    PRINT 'Added Description to WorkflowSteps';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkflowSteps') AND name = 'OnRejectAction')
BEGIN
    ALTER TABLE [WorkflowSteps] ADD [OnRejectAction] nvarchar(max) NOT NULL DEFAULT N'Cancel';
    PRINT 'Added OnRejectAction to WorkflowSteps';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkflowSteps') AND name = 'EscalationHours')
BEGIN
    ALTER TABLE [WorkflowSteps] ADD [EscalationHours] int NULL;
    PRINT 'Added EscalationHours to WorkflowSteps';
END
GO

-- === STEP 4: Add new columns to Tasks ===
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'Priority')
BEGIN
    ALTER TABLE [Tasks] ADD [Priority] nvarchar(max) NOT NULL DEFAULT N'Medium';
    PRINT 'Added Priority to Tasks';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'RejectedReason')
BEGIN
    ALTER TABLE [Tasks] ADD [RejectedReason] nvarchar(max) NULL;
    PRINT 'Added RejectedReason to Tasks';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'CompletedAt')
BEGIN
    ALTER TABLE [Tasks] ADD [CompletedAt] datetime2 NULL;
    PRINT 'Added CompletedAt to Tasks';
END
GO

-- === STEP 5: Create TaskStepHistories table (if not exists) ===
IF OBJECT_ID(N'[TaskStepHistories]') IS NULL
BEGIN
    CREATE TABLE [TaskStepHistories] (
        [Id] int NOT NULL IDENTITY,
        [TaskId] int NOT NULL,
        [StepOrder] int NOT NULL,
        [ActedByUserId] int NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Comment] nvarchar(max) NULL,
        [ActedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TaskStepHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskStepHistories_Tasks_TaskId] 
            FOREIGN KEY ([TaskId]) REFERENCES [Tasks] ([TaskId]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskStepHistories_Users_ActedByUserId] 
            FOREIGN KEY ([ActedByUserId]) REFERENCES [Users] ([UserId])
    );
    CREATE INDEX [IX_TaskStepHistories_TaskId] ON [TaskStepHistories] ([TaskId]);
    CREATE INDEX [IX_TaskStepHistories_ActedByUserId] ON [TaskStepHistories] ([ActedByUserId]);
    PRINT 'Created TaskStepHistories table';
END
GO

-- === STEP 6: Register migration in EF history ===
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260418190557_AddWorkflowApproachBAndTaskEnhancements')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260418190557_AddWorkflowApproachBAndTaskEnhancements', N'10.0.5');
    PRINT 'Migration registered in __EFMigrationsHistory';
END
GO

PRINT 'All migration steps completed successfully.';
GO
