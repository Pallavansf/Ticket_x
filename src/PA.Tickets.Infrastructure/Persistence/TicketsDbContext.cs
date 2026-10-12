using System.ComponentModel.DataAnnotations;using System.ComponentModel.DataAnnotations.Schema;using Microsoft.EntityFrameworkCore;using PA.Tickets.Infrastructure.Tickets;using PA.Tickets.Infrastructure.Foundations;
namespace PA.Tickets.Infrastructure.Persistence;
public sealed class TicketsDbContext(DbContextOptions<TicketsDbContext> options):DbContext(options)
{
 public DbSet<CatalogueSubCategoryRow> CatalogueSubCategories=>Set<CatalogueSubCategoryRow>();
 public DbSet<TicketRow> Tickets=>Set<TicketRow>(); public DbSet<CommunicationRow> Communications=>Set<CommunicationRow>(); public DbSet<CommunicationTemplateRow> CommunicationTemplates=>Set<CommunicationTemplateRow>(); public DbSet<CommunicationAttemptRow> CommunicationAttempts=>Set<CommunicationAttemptRow>(); public DbSet<CommunicationProviderConfigurationRow> CommunicationProviderConfigurations=>Set<CommunicationProviderConfigurationRow>(); public DbSet<CommunicationChannelConfigurationRow> CommunicationChannelConfigurations=>Set<CommunicationChannelConfigurationRow>(); public DbSet<NotificationRow> Notifications=>Set<NotificationRow>(); public DbSet<NotificationRuleRow> NotificationRules=>Set<NotificationRuleRow>(); public DbSet<NotificationRuleVersionRow> NotificationRuleVersions=>Set<NotificationRuleVersionRow>(); public DbSet<NotificationExecutionRow> NotificationExecutions=>Set<NotificationExecutionRow>(); public DbSet<AttachmentRow> Attachments=>Set<AttachmentRow>(); public DbSet<AttachmentVersionRow> AttachmentVersions=>Set<AttachmentVersionRow>(); public DbSet<EvidenceRow> Evidence=>Set<EvidenceRow>(); public DbSet<IncidentRow> Incidents=>Set<IncidentRow>(); public DbSet<ProblemRow> Problems=>Set<ProblemRow>(); public DbSet<ChangeRow> Changes=>Set<ChangeRow>(); public DbSet<ChangeImplementationRow> ChangeImplementations=>Set<ChangeImplementationRow>(); public DbSet<EscalationRow> Escalations=>Set<EscalationRow>(); public DbSet<WorkItemRow> WorkItems=>Set<WorkItemRow>(); public DbSet<EventRow> Events=>Set<EventRow>(); public DbSet<AuditRow> Audits=>Set<AuditRow>(); public DbSet<AssignmentRow> Assignments=>Set<AssignmentRow>(); public DbSet<RelationRow> Relations=>Set<RelationRow>(); public DbSet<WaitingReasonRow> WaitingReasons=>Set<WaitingReasonRow>(); public DbSet<AuditChainLockRow> AuditChainLocks=>Set<AuditChainLockRow>(); public DbSet<AdministrationUserRow> AdministrationUsers=>Set<AdministrationUserRow>(); public DbSet<AdministrationDepartmentRow> AdministrationDepartments=>Set<AdministrationDepartmentRow>(); public DbSet<AdministrationTeamRow> AdministrationTeams=>Set<AdministrationTeamRow>(); public DbSet<AdministrationRoleRow> AdministrationRoles=>Set<AdministrationRoleRow>(); public DbSet<AdministrationPermissionRow> AdministrationPermissions=>Set<AdministrationPermissionRow>(); public DbSet<AdministrationUserRoleRow> AdministrationUserRoles=>Set<AdministrationUserRoleRow>(); public DbSet<AdministrationRolePermissionRow> AdministrationRolePermissions=>Set<AdministrationRolePermissionRow>(); public DbSet<AdministrationUserClientRow> AdministrationUserClients=>Set<AdministrationUserClientRow>(); public DbSet<CatalogueServiceRow> CatalogueServices=>Set<CatalogueServiceRow>(); public DbSet<CatalogueCategoryRow> CatalogueCategories=>Set<CatalogueCategoryRow>();  public DbSet<CatalogueRequestTypeRow> CatalogueRequestTypes=>Set<CatalogueRequestTypeRow>(); public DbSet<CatalogueFieldRow> CatalogueFields=>Set<CatalogueFieldRow>(); public DbSet<CatalogueFieldOptionRow> CatalogueFieldOptions=>Set<CatalogueFieldOptionRow>(); public DbSet<CatalogueRequestFieldRow> CatalogueRequestFields=>Set<CatalogueRequestFieldRow>(); public DbSet<StatusDefinitionRow> StatusDefinitions=>Set<StatusDefinitionRow>(); public DbSet<StatusTransitionRow> StatusTransitions=>Set<StatusTransitionRow>(); public DbSet<PriorityDefinitionRow> PriorityDefinitions=>Set<PriorityDefinitionRow>();  protected override void OnModelCreating(ModelBuilder b){ var capabilities=DatabaseProviderCapabilities.Resolve(Database.ProviderName); capabilities.ConfigureSequences(b); ConfigureAdministration(b, capabilities); ConfigureCatalogue(b); b.Entity<AuditChainLockRow>(e=>{e.ToTable("PA_TICKETS_AUDIT_CHAIN_LOCK");e.HasKey(x=>new{x.EntityType,x.EntityId});e.Property(x=>x.EntityType).HasColumnName("S_ENTITY_TYPE");e.Property(x=>x.EntityId).HasColumnName("S_ENTITY_ID");});
  b.Entity<NotificationRuleRow>(e=>{e.ToTable("PA_TICKETS_NOTIFICATION_RULE");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_NOTIFICATION_RULE_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_NOTIFICATION_RULE_SEQ.NEXTVAL"));e.Property(x=>x.Code).HasColumnName("S_RULE_CODE");e.Property(x=>x.Name).HasColumnName("S_NAME");e.Property(x=>x.Description).HasColumnName("S_DESCRIPTION");e.Property(x=>x.EventType).HasColumnName("S_EVENT_TYPE");e.Property(x=>x.Enabled).HasColumnName("N_ENABLED");e.Property(x=>x.Priority).HasColumnName("N_PRIORITY");e.Property(x=>x.EffectiveFrom).HasColumnName("D_EFFECTIVE_FROM");e.Property(x=>x.EffectiveTo).HasColumnName("D_EFFECTIVE_TO");e.Property(x=>x.CreatedAt).HasColumnName("D_CREATED_AT");e.Property(x=>x.UpdatedAt).HasColumnName("D_UPDATED_AT");e.Property(x=>x.Version).HasColumnName("N_VERSION");e.Property(x=>x.RowVersion).HasColumnName("N_ROW_VERSION");});
  b.Entity<NotificationRuleVersionRow>(e=>{e.ToTable("PA_TICKETS_NOTIFICATION_RULE_VERSION");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_NOTIFICATION_RULE_VERSION_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_NOTIFICATION_RULE_VERSION_SEQ.NEXTVAL"));e.Property(x=>x.RuleId).HasColumnName("N_RULE_ID");e.Property(x=>x.VersionNo).HasColumnName("N_VERSION_NO");e.Property(x=>x.EventType).HasColumnName("S_EVENT_TYPE");e.Property(x=>x.Priority).HasColumnName("N_PRIORITY");e.Property(x=>x.Condition).HasColumnName("S_CONDITION");e.Property(x=>x.RecipientDefinition).HasColumnName("S_RECIPIENT_DEFINITION");e.Property(x=>x.ChannelDefinition).HasColumnName("S_CHANNEL_DEFINITION");e.Property(x=>x.TemplateCode).HasColumnName("S_TEMPLATE_CODE");e.Property(x=>x.DeduplicationScope).HasColumnName("S_DEDUPLICATION_SCOPE");e.Property(x=>x.Enabled).HasColumnName("N_ENABLED");e.Property(x=>x.Status).HasColumnName("S_STATUS");e.Property(x=>x.EffectiveFrom).HasColumnName("D_EFFECTIVE_FROM");e.Property(x=>x.EffectiveTo).HasColumnName("D_EFFECTIVE_TO");e.Property(x=>x.CreatedBy).HasColumnName("N_CREATED_BY");e.Property(x=>x.CreatedAt).HasColumnName("D_CREATED_AT");e.Property(x=>x.PublishedAt).HasColumnName("D_PUBLISHED_AT");e.Property(x=>x.PublishedBy).HasColumnName("N_PUBLISHED_BY");e.Property(x=>x.RetiredAt).HasColumnName("D_RETIRED_AT");e.Property(x=>x.RetiredBy).HasColumnName("N_RETIRED_BY");e.Property(x=>x.RowVersion).HasColumnName("N_ROW_VERSION");e.HasOne(x=>x.Rule).WithMany(x=>x.Versions).HasForeignKey(x=>x.RuleId);});
  b.Entity<NotificationExecutionRow>(e=>{e.ToTable("PA_TICKETS_NOTIFICATION_EXECUTION");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_NOTIFICATION_EXECUTION_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_NOTIFICATION_EXECUTION_SEQ.NEXTVAL"));e.Property(x=>x.TicketId).HasColumnName("N_TICKET_ID");e.Property(x=>x.EventId).HasColumnName("N_EVENT_ID");e.Property(x=>x.RuleId).HasColumnName("N_RULE_ID");e.Property(x=>x.RuleVersionId).HasColumnName("N_RULE_VERSION_ID");e.Property(x=>x.Status).HasColumnName("S_STATUS");e.Property(x=>x.EvaluatedAt).HasColumnName("D_EVALUATED_AT");e.Property(x=>x.Matched).HasColumnName("N_MATCHED");e.Property(x=>x.SkipReason).HasColumnName("S_SKIP_REASON");e.Property(x=>x.CreatedCommunicationCount).HasColumnName("N_CREATED_COMMUNICATION_COUNT");e.Property(x=>x.CorrelationId).HasColumnName("S_CORRELATION_ID");e.Property(x=>x.CreatedAt).HasColumnName("D_CREATED_AT");e.Property(x=>x.CompletedAt).HasColumnName("D_COMPLETED_AT");e.Property(x=>x.FailureCode).HasColumnName("S_FAILURE_CODE");e.Property(x=>x.FailureReason).HasColumnName("S_FAILURE_REASON");e.Property(x=>x.RuleCode).HasColumnName("S_RULE_CODE");e.Property(x=>x.TemplateCode).HasColumnName("S_TEMPLATE_CODE");e.Property(x=>x.SelectedChannel).HasColumnName("S_SELECTED_CHANNEL");e.Property(x=>x.DedupeKey).HasColumnName("S_DEDUPE_KEY");e.Property(x=>x.NotificationContext).HasColumnName("S_NOTIFICATION_CONTEXT");});
  b.Entity<CommunicationTemplateRow>(e=>{e.ToTable("PA_TICKETS_COMMUNICATION_TEMPLATE");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_COMMUNICATION_TEMPLATE_SEQ.NEXTVAL"));e.HasIndex(x=>new{x.Code,x.Channel,x.Version}).IsUnique();});
  b.Entity<CommunicationProviderConfigurationRow>(e=>{e.ToTable("PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_COMM_PROVIDER_CONFIG_SEQ.NEXTVAL"));e.HasIndex(x=>new{x.ProviderCode,x.Channel}).IsUnique();});
  b.Entity<CommunicationChannelConfigurationRow>(e=>{e.ToTable("PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_COMM_CHANNEL_CONFIG_SEQ.NEXTVAL"));e.HasIndex(x=>x.Channel).IsUnique();});
  b.Entity<CommunicationRow>(e=>{e.ToTable("PA_TICKETS_COMMUNICATION");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_COMMUNICATION_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_COMMUNICATION_SEQ.NEXTVAL"));e.Property(x=>x.TicketId).HasColumnName("N_TICKET_ID");e.Property(x=>x.WorkItemId).HasColumnName("N_WORK_ITEM_ID");e.Property(x=>x.NotificationExecutionId).HasColumnName("N_NOTIFICATION_EXECUTION_ID");e.Property(x=>x.NotificationRuleId).HasColumnName("N_NOTIFICATION_RULE_ID");e.Property(x=>x.NotificationRuleVersionId).HasColumnName("N_NOTIFICATION_RULE_VERSION_ID");e.Property(x=>x.EventId).HasColumnName("N_EVENT_ID");e.Property(x=>x.CommunicationType).HasColumnName("S_COMMUNICATION_TYPE");e.Property(x=>x.Channel).HasColumnName("S_CHANNEL");e.Property(x=>x.ProviderCode).HasColumnName("S_PROVIDER_CODE");e.Property(x=>x.TemplateCode).HasColumnName("S_TEMPLATE_CODE");e.Property(x=>x.Status).HasColumnName("S_STATUS");e.Property(x=>x.Recipient).HasColumnName("S_TO_ADDRESS");e.Property(x=>x.Subject).HasColumnName("S_SUBJECT");e.Property(x=>x.Body).HasColumnName("S_BODY");e.Property(x=>x.StructuredPayload).HasColumnName("S_STRUCTURED_PAYLOAD");e.Property(x=>x.ExternalReference).HasColumnName("S_EXTERNAL_REFERENCE");e.Property(x=>x.RetryCount).HasColumnName("N_RETRY_COUNT");e.Property(x=>x.FailureReason).HasColumnName("S_FAILURE_REASON");e.Property(x=>x.ScheduledAt).HasColumnName("D_SCHEDULED_AT");e.Property(x=>x.CommunicationAt).HasColumnName("D_COMMUNICATION_AT");e.Property(x=>x.RecordedAt).HasColumnName("D_RECORDED_AT");e.Property(x=>x.RecordedBy).HasColumnName("N_RECORDED_BY");e.Property(x=>x.CorrelationId).HasColumnName("S_CORRELATION_ID");e.Property(x=>x.IdempotencyKey).HasColumnName("S_IDEMPOTENCY_KEY");});

  b.Entity<TicketRow>(e=>{e.ToTable("PA_TICKETS");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_TICKET_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_SEQ.NEXTVAL"));e.Property(x=>x.Number).HasColumnName("S_TICKET_NO");e.Property(x=>x.TypeId).HasColumnName("N_TICKET_TYPE_ID");e.Property(x=>x.ServiceId).HasColumnName("N_SERVICE_ID");e.Property(x=>x.CategoryId).HasColumnName("N_CATEGORY_ID");e.Property(x=>x.RequestTypeId).HasColumnName("N_REQUEST_TYPE_ID");e.Property(x=>x.ClientId).HasColumnName("N_CLIENT_ID");e.Property(x=>x.RequestedBy).HasColumnName("N_REQUESTED_BY");e.Property(x=>x.Subject).HasColumnName("S_SUBJECT");e.Property(x=>x.Description).HasColumnName("S_DESCRIPTION");e.Property(x=>x.PriorityId).HasColumnName("N_PRIORITY_ID");e.Property(x=>x.StatusId).HasColumnName("N_STATUS_ID");e.Property(x=>x.CurrentDepartmentId).HasColumnName("N_CURRENT_DEPARTMENT_ID");e.Property(x=>x.CurrentTeamId).HasColumnName("N_CURRENT_TEAM_ID");e.Property(x=>x.CurrentAssignee).HasColumnName("N_CURRENT_ASSIGNEE");e.Property(x=>x.ResolvedAt).HasColumnName("D_RESOLVED_AT");e.Property(x=>x.ClosedAt).HasColumnName("D_CLOSED_AT");e.Property(x=>x.CreatedAt).HasColumnName("D_CREATED_AT");e.Property(x=>x.CreatedBy).HasColumnName("N_CREATED_BY");e.Property(x=>x.EvidenceCount).HasColumnName("N_EVIDENCE_COUNT");e.Property(x=>x.ApprovalRequired).HasColumnName("N_APPROVAL_REQUIRED");e.Property(x=>x.Approved).HasColumnName("N_APPROVED");e.Property(x=>x.LastExecutor).HasColumnName("N_LAST_EXECUTOR");e.HasMany(x=>x.WorkItems).WithOne().HasForeignKey(x=>x.TicketId);});
  b.Entity<WorkItemRow>(e=>{e.ToTable("PA_TICKETS_WORK_ITEM");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_WORK_ITEM_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_WORK_ITEM_SEQ.NEXTVAL"));e.Property(x=>x.TicketId).HasColumnName("N_TICKET_ID");e.Property(x=>x.Number).HasColumnName("S_WORK_ITEM_NO");e.Property(x=>x.ToDepartmentId).HasColumnName("N_TO_DEPARTMENT_ID");e.Property(x=>x.ToTeamId).HasColumnName("N_TO_TEAM_ID");e.Property(x=>x.AssignedToUserId).HasColumnName("N_TO_USER_ID");e.Property(x=>x.PriorityId).HasColumnName("N_PRIORITY_ID");e.Property(x=>x.Status).HasColumnName("S_STATUS_CODE");e.Property(x=>x.WaitingFor).HasColumnName("S_WAITING_FOR");e.Property(x=>x.WaitingComment).HasColumnName("S_WAITING_COMMENT");e.Property(x=>x.WaitingEvidenceReference).HasColumnName("S_WAITING_EVIDENCE");e.Property(x=>x.WaitingExpectedDate).HasColumnName("D_WAITING_EXPECTED_AT");e.Property(x=>x.WaitingMinutes).HasColumnName("N_WAITING_MINUTES");e.Property(x=>x.WaitingStartedAt).HasColumnName("D_WAITING_STARTED_AT");e.Property(x=>x.WaitingResumedAt).HasColumnName("D_WAITING_RESUMED_AT");e.Property(x=>x.StartedAt).HasColumnName("D_STARTED_AT");e.Property(x=>x.CompletedAt).HasColumnName("D_COMPLETED_AT");e.Property(x=>x.CreatedAt).HasColumnName("D_CREATED_AT");e.Property(x=>x.CreatedBy).HasColumnName("N_CREATED_BY");});
  b.Entity<WaitingReasonRow>(e=>{e.ToTable("PA_TICKETS_WAITING_REASON");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_WAITING_REASON_ID");e.Property(x=>x.Code).HasColumnName("S_REASON_CODE");e.Property(x=>x.Active).HasColumnName("N_ACTIVE");e.Property(x=>x.EffectiveFrom).HasColumnName("D_EFFECTIVE_FROM");e.Property(x=>x.EffectiveTo).HasColumnName("D_EFFECTIVE_TO");e.Property(x=>x.PauseSla).HasColumnName("N_PAUSE_SLA");e.Property(x=>x.RequireComment).HasColumnName("N_REQUIRE_COMMENT");e.Property(x=>x.RequireEvidence).HasColumnName("N_REQUIRE_EVIDENCE");e.Property(x=>x.RequireExpectedDate).HasColumnName("N_REQUIRE_EXPECTED_DATE");e.Property(x=>x.AllowedFromStatuses).HasColumnName("S_ALLOWED_FROM_STATUSES");});
  b.Entity<EventRow>(e=>{e.ToTable("PA_TICKETS_EVENT");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_EVENT_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_EVENT_SEQ.NEXTVAL"));e.Property(x=>x.TicketId).HasColumnName("N_TICKET_ID");e.Property(x=>x.WorkItemId).HasColumnName("N_WORK_ITEM_ID");e.Property(x=>x.Type).HasColumnName("S_EVENT_TYPE");e.Property(x=>x.Category).HasColumnName("S_EVENT_CATEGORY");e.Property(x=>x.ActorId).HasColumnName("N_ACTOR_USER_ID");e.Property(x=>x.OldValue).HasColumnName("S_OLD_VALUE");e.Property(x=>x.NewValue).HasColumnName("S_NEW_VALUE");e.Property(x=>x.Reason).HasColumnName("S_REASON");e.Property(x=>x.At).HasColumnName("D_EVENT_AT");});
  b.Entity<AuditRow>(e=>{e.ToTable("PA_TICKETS_AUDIT");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_AUDIT_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_AUDIT_SEQ.NEXTVAL"));e.Property(x=>x.EntityType).HasColumnName("S_ENTITY_TYPE");e.Property(x=>x.EntityId).HasColumnName("S_ENTITY_ID");e.Property(x=>x.Action).HasColumnName("S_ACTION");e.Property(x=>x.ActorId).HasColumnName("N_ACTOR_USER_ID");e.Property(x=>x.At).HasColumnName("D_AUDIT_AT");e.Property(x=>x.OldValue).HasColumnName("S_OLD_VALUE");e.Property(x=>x.NewValue).HasColumnName("S_NEW_VALUE");e.Property(x=>x.Reason).HasColumnName("S_REASON");e.Property(x=>x.Hash).HasColumnName("S_HASH");e.Property(x=>x.PreviousHash).HasColumnName("S_PREVIOUS_HASH");});
  b.Entity<AssignmentRow>(e=>{e.ToTable("PA_TICKETS_ASSIGNMENT");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_ASSIGNMENT_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_ASSIGNMENT_SEQ.NEXTVAL"));e.Property(x=>x.TicketId).HasColumnName("N_TICKET_ID");e.Property(x=>x.WorkItemId).HasColumnName("N_WORK_ITEM_ID");e.Property(x=>x.AssignedTo).HasColumnName("N_ASSIGNED_TO");e.Property(x=>x.Type).HasColumnName("S_ASSIGNMENT_TYPE");e.Property(x=>x.AssignedAt).HasColumnName("D_ASSIGNED_AT");e.Property(x=>x.AssignedBy).HasColumnName("N_ASSIGNED_BY");e.Property(x=>x.Reason).HasColumnName("S_REASON");});
  b.Entity<RelationRow>(e=>{e.ToTable("PA_TICKETS_RELATION");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_RELATION_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_RELATION_SEQ.NEXTVAL"));e.Property(x=>x.TicketId).HasColumnName("N_TICKET_ID");e.Property(x=>x.RelatedTicketId).HasColumnName("N_RELATED_TICKET_ID");e.Property(x=>x.Type).HasColumnName("S_RELATION_TYPE");e.Property(x=>x.Description).HasColumnName("S_DESCRIPTION");e.Property(x=>x.CreatedAt).HasColumnName("D_CREATED_AT");e.Property(x=>x.CreatedBy).HasColumnName("N_CREATED_BY");});
}
 private static void ConfigureAdministration(ModelBuilder b, IDatabaseProviderCapabilities capabilities)
 {
  b.Entity<AdministrationUserRow>().HasKey(x=>x.Id);
  b.Entity<AdministrationDepartmentRow>().HasKey(x=>x.Id);
  b.Entity<AdministrationTeamRow>().HasKey(x=>x.Id);
  b.Entity<AdministrationRoleRow>().HasKey(x=>x.Id);
  b.Entity<AdministrationPermissionRow>().HasKey(x=>x.Id);
  b.Entity<AdministrationUserRoleRow>().HasKey(x=>x.Id);
  b.Entity<AdministrationRolePermissionRow>().HasKey(x=>x.Id);
  b.Entity<AdministrationUserClientRow>().HasKey(x=>x.Id);
  if (capabilities.Kind == DatabaseProviderKind.Oracle)
  {
   b.Entity<AdministrationUserRow>().Property(x=>x.Active).HasConversion<int>();
   b.Entity<AdministrationDepartmentRow>().Property(x=>x.Active).HasConversion<int>();
   b.Entity<AdministrationTeamRow>().Property(x=>x.Active).HasConversion<int>();
   b.Entity<AdministrationRoleRow>().Property(x=>x.SystemRole).HasConversion<int>();
   b.Entity<AdministrationRoleRow>().Property(x=>x.Active).HasConversion<int>();
   b.Entity<AdministrationPermissionRow>().Property(x=>x.Active).HasConversion<int>();
   b.Entity<AdministrationUserRoleRow>().Property(x=>x.Active).HasConversion<int>();
   b.Entity<AdministrationUserClientRow>().Property(x=>x.Active).HasConversion<int>();
  }
 }
 private void ConfigureCatalogue(ModelBuilder b)
 {
  b.Entity<TicketRow>().Property<long?>("SubCategoryId").HasColumnName("N_SUB_CATEGORY_ID");
  b.Entity<CatalogueCategoryRow>().HasAlternateKey(x=>new{x.ServiceId,x.Id});
  b.Entity<CatalogueSubCategoryRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_SUB_CATEGORY_SEQ.NEXTVAL"));e.Property(x=>x.Active).HasColumnName("N_ACTIVE");e.HasIndex(x=>new{x.CategoryId,x.Code}).IsUnique();e.HasIndex(x=>new{x.CategoryId,x.Name}).IsUnique();e.HasAlternateKey(x=>new{x.CategoryId,x.Id});});
  b.Entity<CatalogueSubCategoryRow>().HasOne<CatalogueCategoryRow>().WithMany().HasForeignKey(x=>x.CategoryId).HasPrincipalKey(x=>x.Id);
  b.Entity<CatalogueRequestTypeRow>().HasOne<CatalogueCategoryRow>().WithMany().HasForeignKey(x=>new{x.ServiceId,x.CategoryId}).HasPrincipalKey(x=>new{x.ServiceId,x.Id});
  b.Entity<CatalogueRequestTypeRow>().HasOne<CatalogueSubCategoryRow>().WithMany().HasForeignKey(x=>x.SubCategoryId).HasPrincipalKey(x=>x.Id);
  b.Entity<TicketRow>().HasOne<CatalogueSubCategoryRow>().WithMany().HasForeignKey("SubCategoryId").HasPrincipalKey(x=>x.Id);
   b.Entity<CatalogueServiceRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_SERVICE_SEQ.NEXTVAL"));e.Property(x=>x.Active).HasColumnName("N_ACTIVE");});
  b.Entity<CatalogueCategoryRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_CATEGORY_SEQ.NEXTVAL"));e.Property(x=>x.Active).HasColumnName("N_ACTIVE");});
  b.Entity<CatalogueRequestTypeRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_REQUEST_TYPE_SEQ.NEXTVAL"));e.Property(x=>x.Active).HasColumnName("N_ACTIVE");e.HasIndex(x=>new{x.ServiceId,x.Code,x.VersionNo}).IsUnique();});
  b.Entity<CatalogueFieldRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_FIELD_SEQ.NEXTVAL"));e.Property(x=>x.Active).HasColumnName("N_ACTIVE");});
  b.Entity<CatalogueFieldOptionRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_FIELD_OPTION_SEQ.NEXTVAL"));e.Property(x=>x.Active).HasColumnName("N_ACTIVE");e.HasIndex(x=>new{x.FieldId,x.Code}).IsUnique();});
  b.Entity<CatalogueRequestFieldRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_REQUEST_FIELD_SEQ.NEXTVAL"));e.Property(x=>x.Required).HasColumnName("N_REQUIRED");e.Property(x=>x.Active).HasColumnName("N_ACTIVE");e.HasIndex(x=>new{x.RequestTypeId,x.FieldId}).IsUnique();});
  b.Entity<StatusDefinitionRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_STATUS_SEQ.NEXTVAL"));e.HasIndex(x=>x.Code).IsUnique();e.HasIndex(x=>new{x.Active,x.DisplayOrder});});
  b.Entity<StatusTransitionRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_STATUS_TRANSITION_SEQ.NEXTVAL"));e.HasIndex(x=>new{x.FromCode,x.ToCode}).IsUnique();e.HasIndex(x=>new{x.ToCode,x.Allowed});});
  b.Entity<PriorityDefinitionRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_PRIORITY_SEQ.NEXTVAL"));e.HasIndex(x=>x.Code).IsUnique();e.HasIndex(x=>new{x.Active,x.DisplayOrder});});
    b.Entity<TicketRow>().Property<int?>("RequestTypeVersion").HasColumnName("N_REQUEST_TYPE_VERSION");
    b.Entity<TicketFieldValueRow>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasDefaultValueSql(SequenceDefault("PA_TICKETS_VALUE_SEQ.NEXTVAL"));});
    b.Entity<PA.Tickets.Infrastructure.Execution.IdempotencyRow>(e=>{e.ToTable("PA_TICKETS_IDEMPOTENCY");e.HasKey(x=>x.Id);e.Property(x=>x.Id).HasColumnName("N_IDEMPOTENCY_ID").HasDefaultValueSql(SequenceDefault("PA_TICKETS_IDEMPOTENCY_SEQ.NEXTVAL"));e.Property(x=>x.Scope).HasColumnName("S_SCOPE");e.Property(x=>x.Key).HasColumnName("S_IDEMPOTENCY_KEY");e.Property(x=>x.Fingerprint).HasColumnName("S_REQUEST_FINGERPRINT");e.Property(x=>x.Status).HasColumnName("S_STATUS");e.Property(x=>x.CreatedAt).HasColumnName("D_CREATED_AT");e.Property(x=>x.CompletedAt).HasColumnName("D_COMPLETED_AT");e.Property(x=>x.ActorId).HasColumnName("N_ACTOR_USER_ID");e.Property(x=>x.ResultTicketId).HasColumnName("N_RESULT_TICKET_ID");});
  if(DatabaseProviderCapabilities.Resolve(Database.ProviderName).Kind==DatabaseProviderKind.Oracle)
  {
  b.Entity<CatalogueServiceRow>().Property(x=>x.Active).HasConversion<int>(); b.Entity<CatalogueCategoryRow>().Property(x=>x.Active).HasConversion<int>(); b.Entity<CatalogueSubCategoryRow>().Property(x=>x.Active).HasConversion<int>(); b.Entity<CatalogueRequestTypeRow>().Property(x=>x.Active).HasConversion<int>(); b.Entity<CatalogueRequestTypeRow>().Property(x=>x.EvidenceRequired).HasConversion<int>(); b.Entity<CatalogueFieldRow>().Property(x=>x.Active).HasConversion<int>(); b.Entity<CatalogueFieldOptionRow>().Property(x=>x.Active).HasConversion<int>(); b.Entity<CatalogueRequestFieldRow>().Property(x=>x.Required).HasConversion<int>(); b.Entity<CatalogueRequestFieldRow>().Property(x=>x.Active).HasConversion<int>(); b.Entity<StatusDefinitionRow>().Property(x=>x.IsInitial).HasConversion<int>(); b.Entity<StatusDefinitionRow>().Property(x=>x.IsOpen).HasConversion<int>(); b.Entity<StatusDefinitionRow>().Property(x=>x.IsClosed).HasConversion<int>(); b.Entity<StatusDefinitionRow>().Property(x=>x.IsCancelled).HasConversion<int>(); b.Entity<StatusDefinitionRow>().Property(x=>x.Active).HasConversion<int>(); b.Entity<StatusTransitionRow>().Property(x=>x.Allowed).HasConversion<int>(); b.Entity<PriorityDefinitionRow>().Property(x=>x.Active).HasConversion<int>(); b.Entity<PriorityDefinitionRow>().Property(x=>x.IsDefault).HasConversion<int>();
  }
 }
 private string SequenceDefault(string oracle)=>DatabaseProviderCapabilities.Resolve(Database.ProviderName).SequenceDefaultSql(oracle);
}
public sealed class TicketRow {public long Id{get;set;}public string Number{get;set;}="";public int TypeId{get;set;}public long? ServiceId{get;set;}public long? CategoryId{get;set;}public long? RequestTypeId{get;set;}public long? ClientId{get;set;}public long RequestedBy{get;set;}public string Subject{get;set;}="";public string? Description{get;set;}public long? PriorityId{get;set;}public int StatusId{get;set;}public long? CurrentDepartmentId{get;set;}public long? CurrentTeamId{get;set;}public long? CurrentAssignee{get;set;}public DateTimeOffset? ResolvedAt{get;set;}public DateTimeOffset? ClosedAt{get;set;}public DateTimeOffset CreatedAt{get;set;}public long CreatedBy{get;set;}public int EvidenceCount{get;set;}public int ApprovalRequired{get;set;}public int Approved{get;set;}public long? LastExecutor{get;set;}public List<WorkItemRow> WorkItems{get;set;}=[];}
public sealed class WorkItemRow {public long Id{get;set;}public long TicketId{get;set;}public string Number{get;set;}="";public string PublicId{get;set;}="";public long? ToDepartmentId{get;set;}public long? ToTeamId{get;set;}public long? AssignedToUserId{get;set;}public long? PriorityId{get;set;}public string Status{get;set;}="PENDING";public string? WaitingFor{get;set;}public string? WaitingComment{get;set;}public string? WaitingEvidenceReference{get;set;}public DateTimeOffset? WaitingExpectedDate{get;set;}public int WaitingMinutes{get;set;}public DateTimeOffset? WaitingStartedAt{get;set;}public DateTimeOffset? WaitingResumedAt{get;set;}public DateTimeOffset? StartedAt{get;set;}public DateTimeOffset? CompletedAt{get;set;}public DateTimeOffset CreatedAt{get;set;}public long CreatedBy{get;set;}}
public sealed class EventRow {public long Id{get;set;}public long TicketId{get;set;}public long? WorkItemId{get;set;}public string Type{get;set;}="";public string Category{get;set;}="LIFECYCLE";public long ActorId{get;set;}public string? OldValue{get;set;}public string? NewValue{get;set;}public string? Reason{get;set;}public DateTimeOffset At{get;set;}}
public sealed class AuditRow {public long Id{get;set;}public string EntityType{get;set;}="TICKET";public string EntityId{get;set;}="";public string Action{get;set;}="";public long ActorId{get;set;}public DateTimeOffset At{get;set;}public string? OldValue{get;set;}public string? NewValue{get;set;}public string? Reason{get;set;}public string Hash{get;set;}="";public string? PreviousHash{get;set;}}

[Table("PA_TICKETS_STATUS")]
public sealed class StatusDefinitionRow
{
    [Key, Column("N_STATUS_ID")] public long Id { get; set; }
    [Column("S_STATUS_CODE")] public string Code { get; set; } = "";
    [Column("S_STATUS_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_IS_INITIAL")] public bool IsInitial { get; set; }
    [Column("N_IS_OPEN")] public bool IsOpen { get; set; }
    [Column("N_IS_CLOSED")] public bool IsClosed { get; set; }
    [Column("N_IS_CANCELLED")] public bool IsCancelled { get; set; }
    [Column("N_DISPLAY_ORDER")] public int DisplayOrder { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset? UpdatedAt { get; set; }
    [Column("N_UPDATED_BY")] public long? UpdatedBy { get; set; }
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
}

[Table("PA_TICKETS_STATUS_TRANSITION")]
public sealed class StatusTransitionRow
{
    [Key, Column("N_STATUS_TRANSITION_ID")] public long Id { get; set; }
    [Column("S_FROM_STATUS_CODE")] public string FromCode { get; set; } = "";
    [Column("S_TO_STATUS_CODE")] public string ToCode { get; set; } = "";
    [Column("S_CONDITION")] public string? Condition { get; set; }
    [Column("N_ALLOWED")] public bool Allowed { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
}

[Table("PA_TICKETS_PRIORITY")]
public sealed class PriorityDefinitionRow
{
    [Key, Column("N_PRIORITY_ID")] public long Id { get; set; }
    [Column("S_PRIORITY_CODE")] public string Code { get; set; } = "";
    [Column("S_PRIORITY_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_PRIORITY_LEVEL")] public int Level { get; set; }
    [Column("N_DISPLAY_ORDER")] public int DisplayOrder { get; set; }
    [Column("N_ACTIVE")] public bool Active { get; set; }
    [Column("N_IS_DEFAULT")] public bool IsDefault { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset? UpdatedAt { get; set; }
    [Column("N_UPDATED_BY")] public long? UpdatedBy { get; set; }
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
}
public sealed class AssignmentRow {public long Id{get;set;}public long TicketId{get;set;}public long? WorkItemId{get;set;}public long? AssignedTo{get;set;}public string Type{get;set;}="WORK_ITEM";public DateTimeOffset AssignedAt{get;set;}public long AssignedBy{get;set;}public string? Reason{get;set;}}
public sealed class RelationRow {public long Id{get;set;}public long TicketId{get;set;}public long RelatedTicketId{get;set;}public string Type{get;set;}="";public string? Description{get;set;}public DateTimeOffset CreatedAt{get;set;}public long CreatedBy{get;set;}}

public sealed class CommunicationRow { public long Id{get;set;} public long TicketId{get;set;} public long? WorkItemId{get;set;} [Column("S_PUBLIC_ID")] public string PublicId{get;set;}=""; public string CommunicationType{get;set;}="SYSTEM"; public string? Channel{get;set;} public string? ProviderCode{get;set;} public string? TemplateCode{get;set;} [Column("N_TEMPLATE_VERSION_ID")] public long? TemplateVersionId{get;set;} public string Status{get;set;}="QUEUED"; public string? Recipient{get;set;} public string? Subject{get;set;} public string? Body{get;set;} public string? StructuredPayload{get;set;} public string? ExternalReference{get;set;} public int RetryCount{get;set;} [Column("D_NEXT_RETRY_AT")] public DateTimeOffset? NextRetryAt{get;set;} [Column("S_LEASE_OWNER")] public string? LeaseOwner{get;set;} [Column("D_LEASE_UNTIL")] public DateTimeOffset? LeaseUntil{get;set;} [Column("S_FAILURE_CODE")] public string? FailureCode{get;set;} [Column("S_FAILURE_CLASSIFICATION")] public string? FailureClassification{get;set;} public string? FailureReason{get;set;} [Column("D_SENT_AT")] public DateTimeOffset? SentAt{get;set;} [Column("D_DELIVERED_AT")] public DateTimeOffset? DeliveredAt{get;set;} [Column("D_FAILED_AT")] public DateTimeOffset? FailedAt{get;set;} public DateTimeOffset? ScheduledAt{get;set;} public DateTimeOffset CommunicationAt{get;set;} public DateTimeOffset RecordedAt{get;set;} public long RecordedBy{get;set;} public string? CorrelationId{get;set;} public string? IdempotencyKey{get;set;} [Column("N_NOTIFICATION_EXECUTION_ID")] public long? NotificationExecutionId{get;set;} [Column("N_NOTIFICATION_RULE_ID")] public long? NotificationRuleId{get;set;} [Column("N_NOTIFICATION_RULE_VERSION_ID")] public long? NotificationRuleVersionId{get;set;} [Column("N_EVENT_ID")] public long? EventId{get;set;} }
[Table("PA_TICKETS_COMMUNICATION_TEMPLATE")]
public sealed class CommunicationTemplateRow
{
    [Key, Column("N_TEMPLATE_VERSION_ID")] public long Id { get; set; }
    [Column("S_TEMPLATE_CODE")] public string Code { get; set; } = "";
    [Column("S_CHANNEL")] public string Channel { get; set; } = "";
    [Column("N_VERSION")] public int Version { get; set; }
    [Column("S_SUBJECT")] public string? Subject { get; set; }
    [Column("S_BODY")] public string Body { get; set; } = "";
    [Column("S_STATUS")] public string Status { get; set; } = "DRAFT";
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_PUBLISHED_AT")] public DateTimeOffset? PublishedAt { get; set; }
    [Column("N_PUBLISHED_BY")] public long? PublishedBy { get; set; }
    [Column("D_RETIRED_AT")] public DateTimeOffset? RetiredAt { get; set; }
    [Column("N_RETIRED_BY")] public long? RetiredBy { get; set; }
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
}
[Table("PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG")]
public sealed class CommunicationProviderConfigurationRow
{
    [Key, Column("N_PROVIDER_CONFIG_ID")] public long Id { get; set; }
    [Column("S_PROVIDER_CODE")] public string ProviderCode { get; set; } = "";
    [Column("S_CHANNEL")] public string Channel { get; set; } = "";
    [Column("N_ENABLED")] public bool Enabled { get; set; }
    [Column("N_MAX_ATTEMPTS")] public int MaxAttempts { get; set; }
    [Column("N_RETRY_DELAY_SECONDS")] public int RetryDelaySeconds { get; set; }
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset UpdatedAt { get; set; }
    [Column("N_UPDATED_BY")] public long UpdatedBy { get; set; }
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
}
[Table("PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG")]
public sealed class CommunicationChannelConfigurationRow
{
    [Key, Column("N_CHANNEL_CONFIG_ID")] public long Id { get; set; }
    [Column("S_CHANNEL")] public string Channel { get; set; } = "";
    [Column("N_ENABLED")] public bool Enabled { get; set; }
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset UpdatedAt { get; set; }
    [Column("N_UPDATED_BY")] public long UpdatedBy { get; set; }
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
}
[Table("PA_TICKETS_COMMUNICATION_ATTEMPT")]
public sealed class CommunicationAttemptRow { [Key,Column("N_ATTEMPT_ID")] public long Id{get;set;} [Column("N_COMMUNICATION_ID")] public long CommunicationId{get;set;} [Column("N_ATTEMPT_NO")] public int AttemptNumber{get;set;} [Column("D_STARTED_AT")] public DateTimeOffset StartedAt{get;set;} [Column("D_COMPLETED_AT")] public DateTimeOffset CompletedAt{get;set;} [Column("S_PROVIDER")] public string Provider{get;set;}=""; [Column("S_PROVIDER_MESSAGE_ID")] public string? ProviderMessageId{get;set;} [Column("S_RESULT")] public string Result{get;set;}=""; [Column("S_FAILURE_CLASSIFICATION")] public string? FailureClassification{get;set;} [Column("S_FAILURE_CODE")] public string? FailureCode{get;set;} [Column("S_FAILURE_REASON")] public string? FailureReason{get;set;} [Column("S_CORRELATION_ID")] public string? CorrelationId{get;set;} }
