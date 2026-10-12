using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class AttachmentService(TicketsDbContext db,IFileStorage storage,IContentScanner scanner,IConfiguration configuration,AuditChainCoordinator auditLocks,ITicketAccessAuthorizer authorizer,ProviderTransactionRetry retry) : IAttachmentService
{
    private const long MaximumUploadBytes=50L*1024*1024;
    private long MaxSize => Math.Min(configuration.GetValue("Storage:MaxFileSizeBytes",MaximumUploadBytes),MaximumUploadBytes);

    public Task<AttachmentResult> CreateAsync(CreateAttachmentCommand command,long actor,CancellationToken ct)
    {
        var contentStart = command.Content.CanSeek ? command.Content.Position : (long?)null;
        return retry.ExecuteAsync<AttachmentResult>(db, async () =>
        {
            if (contentStart.HasValue) command.Content.Position = contentStart.Value;
            if (actor <= 0 || string.IsNullOrWhiteSpace(command.IdempotencyKey)) throw new ArgumentException("Actor and idempotency key are required.");
            var ticket=await db.Tickets.SingleOrDefaultAsync(x=>x.Id==command.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
            await authorizer.EnsureCanMutateAsync(actor,Ticket.Restore(ticket.Id,ticket.Number,(TicketType)ticket.TypeId,(TicketStatus)ticket.StatusId,ticket.RequestedBy,ticket.CreatedBy,ticket.Subject,ticket.Description,ticket.ServiceId,ticket.CategoryId,ticket.RequestTypeId,ticket.PriorityId,ticket.CurrentDepartmentId,ticket.CurrentTeamId,ticket.CurrentAssignee,ticket.CreatedAt,ticket.ResolvedAt,ticket.ClosedAt,ticket.ApprovalRequired==1,ticket.Approved==1,ticket.EvidenceCount,ticket.LastExecutor,[]),ct);
            if(command.WorkItemId is long workItemId&&!await db.WorkItems.AnyAsync(x=>x.Id==workItemId&&x.TicketId==command.TicketId,ct))
                throw new KeyNotFoundException("Work item was not found for this ticket.");
            if (await db.Attachments.AnyAsync(x => x.IdempotencyKey == command.IdempotencyKey, ct)) throw new InvalidOperationException("Attachment request was already processed.");
            PreparedUpload prepared;
            try{prepared=await PrepareUploadAsync(command.FileName,command.ContentType,command.Content,ct);}
            catch(Exception ex) when(ex is ContentRejectedException or ContentScannerUnavailableException or AttachmentTooLargeException or ArgumentException)
            {
                await RecordRejectedUploadAsync(command.TicketId,command.IdempotencyKey,actor,command.CorrelationId,command.ContentType,FailureCode(ex),ct);
                throw;
            }
            await using var preparedUpload=prepared;
            var id = await NextAsync("PA_TICKETS_ATTACHMENT_SEQ", ct); var versionId = await NextAsync("PA_TICKETS_ATTACHMENT_VERSION_SEQ", ct);
            var key = $"attachments/{id}/{versionId}";
            try
            {
                await using var uploadContent=preparedUpload.OpenRead();
                var stored=await storage.PutAsync(key,preparedUpload.FileName,preparedUpload.DetectedContentType,uploadContent,MaxSize,ct);
                EnsureStoredContentMatches(preparedUpload,stored);
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var now = DateTimeOffset.UtcNow;
                db.Attachments.Add(new AttachmentRow { Id=id, TicketId=command.TicketId, WorkItemId=command.WorkItemId, Type=command.AttachmentType, FileName=preparedUpload.FileName, ContentType=preparedUpload.DetectedContentType, FileSize=stored.Metadata.ContentLength, StorageProvider=stored.Metadata.Provider, StorageReference=stored.Metadata.ObjectKey, Sha256Hash=stored.Metadata.Sha256Hash, Classification=command.Classification, Description=command.Description, IdempotencyKey=command.IdempotencyKey, CurrentVersion=1, Active=true, UploadedAt=now, UploadedBy=actor });
                db.AttachmentVersions.Add(new AttachmentVersionRow { Id=versionId, AttachmentId=id, VersionNo=1, FileName=preparedUpload.FileName, ContentType=preparedUpload.DetectedContentType, FileSize=stored.Metadata.ContentLength, StorageProvider=stored.Metadata.Provider, StorageReference=stored.Metadata.ObjectKey, Sha256Hash=stored.Metadata.Sha256Hash, Status="CURRENT", IdempotencyKey=command.IdempotencyKey, UploadedAt=now, UploadedBy=actor });
                await AddAudit("ATTACHMENT",id.ToString(),"ATTACHMENT_CREATED",actor,command.CorrelationId,ct,preparedUpload.AuditDetails(command.ContentType,stored.Metadata.Sha256Hash));
                db.Events.Add(new EventRow{TicketId=command.TicketId,Type="ATTACHMENT_CREATED",Category="ATTACHMENT",ActorId=actor,NewValue=id.ToString(),Reason=command.CorrelationId,At=now});
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                return new(new(id,command.TicketId,command.WorkItemId,command.AttachmentType,preparedUpload.FileName,preparedUpload.DetectedContentType,stored.Metadata.ContentLength,stored.Metadata.Provider,stored.Metadata.ObjectKey,stored.Metadata.Sha256Hash,1,true,now,actor),new(versionId,id,1,preparedUpload.FileName,preparedUpload.DetectedContentType,stored.Metadata.ContentLength,stored.Metadata.Provider,stored.Metadata.ObjectKey,stored.Metadata.Sha256Hash,"CURRENT",null,now,actor));
            }
            catch { await storage.DeleteAsync(key, CancellationToken.None); throw; }
        }, ct);
    }

    public Task<AttachmentResult> CreateVersionAsync(long attachmentId,string fileName,string? contentType,Stream content,string changeReason,string idempotencyKey,string? correlationId,long actor,CancellationToken ct)
    {
        var contentStart = content.CanSeek ? content.Position : (long?)null;
        return retry.ExecuteAsync<AttachmentResult>(db, async () =>
        {
            if (contentStart.HasValue) content.Position = contentStart.Value;
            if(actor<=0||string.IsNullOrWhiteSpace(idempotencyKey))throw new ArgumentException("Actor and idempotency key are required.");
            var authorizedAttachment=await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==attachmentId,ct)??throw new KeyNotFoundException("Attachment was not found.");
            var authorizedTicket=await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==authorizedAttachment.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
            await authorizer.EnsureCanMutateAsync(actor,ToTicket(authorizedTicket),ct);
            if (await db.AttachmentVersions.AnyAsync(x => x.IdempotencyKey == idempotencyKey, ct)) throw new InvalidOperationException("Attachment version request was already processed.");
            PreparedUpload prepared;
            try{prepared=await PrepareUploadAsync(fileName,contentType,content,ct);}
            catch(Exception ex) when(ex is ContentRejectedException or ContentScannerUnavailableException or AttachmentTooLargeException or ArgumentException)
            {
                await RecordRejectedUploadAsync(authorizedTicket.Id,idempotencyKey,actor,correlationId,contentType,FailureCode(ex),ct);
                throw;
            }
            await using var preparedUpload=prepared;
            var postgres = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
            string? storageKey = null;
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
                var attachment = postgres
                    ? await db.Attachments.FromSqlInterpolated($@"select * from ""PA_TICKETS_ATTACHMENT"" where ""N_ATTACHMENT_ID"" = {attachmentId} for update").SingleOrDefaultAsync(ct)
                    : await db.Attachments.SingleOrDefaultAsync(x=>x.Id==attachmentId,ct);
                if (attachment is null) throw new KeyNotFoundException("Attachment was not found.");
                var ticket = await db.Tickets.SingleOrDefaultAsync(x=>x.Id==attachment.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
                await authorizer.EnsureCanMutateAsync(actor,ToTicket(ticket),ct);
                var current = await db.AttachmentVersions.Where(x=>x.AttachmentId==attachmentId).OrderByDescending(x=>x.VersionNo).FirstOrDefaultAsync(ct);
                var versionNo=(current?.VersionNo??0)+1;
                var versionId=await NextAsync("PA_TICKETS_ATTACHMENT_VERSION_SEQ",ct);
                storageKey=$"attachments/{attachmentId}/{versionId}";
                await using var uploadContent=preparedUpload.OpenRead();
                var stored=await storage.PutAsync(storageKey,preparedUpload.FileName,preparedUpload.DetectedContentType,uploadContent,MaxSize,ct);
                EnsureStoredContentMatches(preparedUpload,stored); var now=DateTimeOffset.UtcNow;
                if(current is not null) current.Status="SUPERSEDED";
                db.AttachmentVersions.Add(new AttachmentVersionRow{Id=versionId,AttachmentId=attachmentId,VersionNo=versionNo,FileName=preparedUpload.FileName,ContentType=preparedUpload.DetectedContentType,FileSize=stored.Metadata.ContentLength,StorageProvider=stored.Metadata.Provider,StorageReference=stored.Metadata.ObjectKey,Sha256Hash=stored.Metadata.Sha256Hash,Status="CURRENT",ChangeReason=changeReason,IdempotencyKey=idempotencyKey,UploadedAt=now,UploadedBy=actor}); attachment.CurrentVersion=versionNo;
                await AddAudit("ATTACHMENT",attachmentId.ToString(),"ATTACHMENT_VERSION_CREATED",actor,correlationId,ct,preparedUpload.AuditDetails(contentType,stored.Metadata.Sha256Hash)); db.Events.Add(new EventRow{TicketId=attachment.TicketId,Type="ATTACHMENT_VERSION_CREATED",Category="ATTACHMENT",ActorId=actor,NewValue=versionNo.ToString(),Reason=correlationId,At=now}); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                return new(new(attachment.Id,attachment.TicketId,attachment.WorkItemId,attachment.Type,preparedUpload.FileName,preparedUpload.DetectedContentType,stored.Metadata.ContentLength,stored.Metadata.Provider,stored.Metadata.ObjectKey,stored.Metadata.Sha256Hash,versionNo,true,attachment.UploadedAt,attachment.UploadedBy),new(versionId,attachmentId,versionNo,preparedUpload.FileName,preparedUpload.DetectedContentType,stored.Metadata.ContentLength,stored.Metadata.Provider,stored.Metadata.ObjectKey,stored.Metadata.Sha256Hash,"CURRENT",changeReason,now,actor));
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(storageKey)) await storage.DeleteAsync(storageKey, CancellationToken.None);
                throw;
            }
        }, ct);
    }

    private async Task<PreparedUpload> PrepareUploadAsync(string originalFileName,string? declaredContentType,Stream source,CancellationToken ct)
    {
        var fileName=ValidateFileName(originalFileName);
        var maxSize=MaxSize;
        if(maxSize<=0)throw new InvalidOperationException("The maximum attachment size is not configured correctly.");
        if(source.CanSeek&&source.Length-source.Position>maxSize)throw new AttachmentTooLargeException();
        var path=Path.Combine(Path.GetTempPath(),$"pa-tickets-{Guid.NewGuid():N}.quarantine");
        try
        {
            long length=0;
            using var hash=System.Security.Cryptography.IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous|FileOptions.SequentialScan))
            {
                var buffer=new byte[81920];
                int read;
                while((read=await source.ReadAsync(buffer,ct))>0)
                {
                    length+=read;
                    if(length>maxSize)throw new AttachmentTooLargeException();
                    hash.AppendData(buffer,0,read);
                    await output.WriteAsync(buffer.AsMemory(0,read),ct);
                }
                await output.FlushAsync(ct);
            }
            if(length==0)throw new InvalidOperationException("A non-empty file is required.");
            var sha256=Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            string? detectedContentType;
            await using(var inspection=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.Asynchronous|FileOptions.SequentialScan))
                detectedContentType=FileContentTypeDetector.Detect(inspection);
            if(!AttachmentContentPolicy.IsAllowed(fileName,declaredContentType,detectedContentType))
                throw new ContentRejectedException("The uploaded file type does not match the permitted content policy.");

            ContentScanResult scan;
            var timeoutSeconds=configuration.GetValue("Storage:ScanTimeoutSeconds",30);
            if(timeoutSeconds<=0||timeoutSeconds>300)throw new InvalidOperationException("The content scanner timeout configuration is invalid.");
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                try
                {
                    await using var scanStream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.Asynchronous|FileOptions.SequentialScan);
                    scan=await scanner.ScanAsync(scanStream,timeout.Token);
                }
                catch(OperationCanceledException) when(!ct.IsCancellationRequested)
                {
                    scan=new(ContentScanStatus.Timeout,null,"SCANNER_TIMEOUT");
                }
                catch(Exception) when(!ct.IsCancellationRequested)
                {
                    scan=new(ContentScanStatus.Error,null,"SCANNER_ERROR");
                }
            }
            if(scan.Status==ContentScanStatus.Rejected)
                throw new ContentRejectedException("The uploaded file was rejected by the content scanner.");
            if(scan.Status!=ContentScanStatus.Clean)
                throw new ContentScannerUnavailableException();
            return new PreparedUpload(path,fileName,detectedContentType!,length,sha256,scan.ScannerVersion,DateTimeOffset.UtcNow);
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    private static string ValidateFileName(string fileName)
    {
        if(string.IsNullOrWhiteSpace(fileName)||fileName.Length>255||fileName.IndexOf('\0')>=0||
           fileName.Any(char.IsControl)||fileName.Contains('/')||fileName.Contains('\\')||
           fileName.Contains(':')||fileName.Contains("..",StringComparison.Ordinal)||
           Path.IsPathRooted(fileName)||!string.Equals(Path.GetFileName(fileName),fileName,StringComparison.Ordinal))
            throw new ArgumentException("Invalid attachment file name.");
        return fileName.Trim();
    }

    private static void TryDelete(string path)
    {
        if(File.Exists(path))File.Delete(path);
    }

    private static void EnsureStoredContentMatches(PreparedUpload prepared,StoredObject stored)
    {
        if(stored.Metadata.ContentLength!=prepared.Length||
           !string.Equals(stored.Metadata.Sha256Hash,prepared.Sha256,StringComparison.OrdinalIgnoreCase)||
           !string.Equals(stored.Metadata.ContentType,prepared.DetectedContentType,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stored attachment content did not match the inspected upload.");
    }

    private async Task RecordRejectedUploadAsync(long ticketId,string idempotencyKey,long actor,string? correlationId,string? declaredContentType,string failureCode,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var details=System.Text.Json.JsonSerializer.Serialize(new{ticketId,declaredMimeType=declaredContentType,scanStatus=failureCode,requestFingerprint=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idempotencyKey))).ToLowerInvariant(),occurredAtUtc=DateTimeOffset.UtcNow.UtcDateTime});
        await AddAudit("TICKET",ticketId.ToString(),"ATTACHMENT_UPLOAD_REJECTED",actor,correlationId,ct,details);
        db.Events.Add(new EventRow{TicketId=ticketId,Type="ATTACHMENT_UPLOAD_REJECTED",Category="SECURITY",ActorId=actor,NewValue=failureCode,Reason=correlationId,At=DateTimeOffset.UtcNow});
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static string FailureCode(Exception exception)=>exception switch
    {
        AttachmentTooLargeException=>"FILE_TOO_LARGE",
        ContentScannerUnavailableException=>"SCANNER_UNAVAILABLE",
        ContentRejectedException=>"CONTENT_REJECTED",
        _=>"INVALID_FILE_METADATA"
    };

    private sealed class PreparedUpload(string path,string fileName,string detectedContentType,long length,string sha256,string? scannerVersion,DateTimeOffset scannedAt):IAsyncDisposable
    {
        public string FileName{get;}=fileName;
        public string DetectedContentType{get;}=detectedContentType;
        public long Length{get;}=length;
        public string Sha256{get;}=sha256;
        public Stream OpenRead()=>new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.Asynchronous|FileOptions.SequentialScan);
        public string AuditDetails(string? declaredContentType,string storedHash)=>System.Text.Json.JsonSerializer.Serialize(new
        {
            declaredMimeType=declaredContentType,
            detectedMimeType=DetectedContentType,
            fileExtension=Path.GetExtension(FileName),
            actualFileSize=Length,
            sha256=storedHash,
            scanStatus=ContentScanStatus.Clean.ToString(),
            scannerVersion,
            scannedAtUtc=scannedAt.UtcDateTime
        });
        public ValueTask DisposeAsync()
        {
            TryDelete(path);
            return ValueTask.CompletedTask;
        }
    }

    public async Task<Attachment> GetAsync(long id,long actor,CancellationToken ct)
    {
        var attachment=await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException("Attachment was not found.");
        await EnsureCanAccessAsync(actor,attachment.TicketId,ct);
        return MapAttachment(attachment);
    }
    public async Task<IReadOnlyCollection<AttachmentVersion>> VersionsAsync(long id,long actor,CancellationToken ct)
    {
        var attachment=await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException("Attachment was not found.");
        await EnsureCanAccessAsync(actor,attachment.TicketId,ct);
        return (await db.AttachmentVersions.AsNoTracking().Where(x=>x.AttachmentId==id).OrderBy(x=>x.VersionNo).ToListAsync(ct)).Select(MapVersion).ToArray();
    }
    public async Task<(AttachmentVersion Version,Stream Content)> DownloadAsync(long id,int version,long actor,CancellationToken ct)
    {
        var attachment=await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException("Attachment was not found.");
        var ticket=await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==attachment.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
        await authorizer.EnsureCanAccessAsync(actor,Ticket.Restore(ticket.Id,ticket.Number,(TicketType)ticket.TypeId,(TicketStatus)ticket.StatusId,ticket.RequestedBy,ticket.CreatedBy,ticket.Subject,ticket.Description,ticket.ServiceId,ticket.CategoryId,ticket.RequestTypeId,ticket.PriorityId,ticket.CurrentDepartmentId,ticket.CurrentTeamId,ticket.CurrentAssignee,ticket.CreatedAt,ticket.ResolvedAt,ticket.ClosedAt,ticket.ApprovalRequired==1,ticket.Approved==1,ticket.EvidenceCount,ticket.LastExecutor,[]),ct);
        var row=await db.AttachmentVersions.AsNoTracking().SingleOrDefaultAsync(x=>x.AttachmentId==id&&x.VersionNo==version,ct)??throw new KeyNotFoundException("Attachment version was not found.");
        var stream=await storage.OpenReadAsync(row.StorageReference!,ct); using var hash=SHA256.Create(); var actual=await hash.ComputeHashAsync(stream,ct); if(!actual.SequenceEqual(Convert.FromHexString(row.Sha256Hash))){await stream.DisposeAsync();throw new InvalidOperationException("Attachment checksum verification failed.");} stream.Position=0; return (MapVersion(row),stream);
    }
    public Task<Evidence> CreateEvidenceAsync(CreateEvidenceCommand command,long actor,CancellationToken ct)
        => retry.ExecuteAsync<Evidence>(db, async () =>
        {
            var ticket=await db.Tickets.SingleOrDefaultAsync(x=>x.Id==command.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");await authorizer.EnsureCanMutateAsync(actor,ToTicket(ticket),ct);
            if(command.WorkItemId is long workItemId&&!await db.WorkItems.AnyAsync(x=>x.Id==workItemId&&x.TicketId==command.TicketId,ct))throw new KeyNotFoundException("Work item was not found for this ticket.");
            if(command.AttachmentId is long attachmentId)
            {
                var attachment=await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==attachmentId&&x.TicketId==command.TicketId&&x.Active,ct)??throw new KeyNotFoundException("Accepted attachment was not found.");
                if(!await db.AttachmentVersions.AsNoTracking().AnyAsync(x=>x.AttachmentId==attachmentId&&x.VersionNo==attachment.CurrentVersion&&x.Status=="CURRENT",ct))
                    throw new InvalidOperationException("Evidence can only reference an accepted current attachment version.");
            }
            var id=await NextAsync("PA_TICKETS_EVIDENCE_SEQ",ct);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var now=DateTimeOffset.UtcNow;var row=new EvidenceRow{Id=id,TicketId=command.TicketId,WorkItemId=command.WorkItemId,AttachmentId=command.AttachmentId,Code=command.Code,Type=command.Type,Description=command.Description,ControlReference=command.ControlReference,Status="CREATED",ReviewDueAt=command.ReviewDueAt,RetentionUntil=command.RetentionUntil,CreatedAt=now,CreatedBy=actor};db.Evidence.Add(row);await AddAudit("EVIDENCE",id.ToString(),"EVIDENCE_CREATED",actor,command.CorrelationId,ct);db.Events.Add(new EventRow{TicketId=command.TicketId,Type="EVIDENCE_CREATED",Category="EVIDENCE",ActorId=actor,NewValue=id.ToString(),Reason=command.CorrelationId,At=now});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return MapEvidence(row);
        }, ct);
    public Task<Evidence> ReviewEvidenceAsync(long id,ReviewEvidenceCommand command,long actor,CancellationToken ct)
        => retry.ExecuteAsync<Evidence>(db, async () =>
        {
            var row=await db.Evidence.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException("Evidence was not found.");await EnsureCanMutateAsync(actor,row.TicketId,ct);EvidencePolicy.ValidateTransition(row.Status,command.Status);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);row.Status=command.Status;row.ReviewedBy=actor;row.ReviewedAt=DateTimeOffset.UtcNow;row.ReviewComment=command.Comment;await AddAudit("EVIDENCE",id.ToString(),"EVIDENCE_REVIEWED",actor,command.Comment,ct);db.Events.Add(new EventRow{TicketId=row.TicketId,Type="EVIDENCE_REVIEWED",Category="EVIDENCE",ActorId=actor,NewValue=command.Status,Reason=command.Comment,At=row.ReviewedAt.Value});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return MapEvidence(row);
        }, ct);
    private async Task EnsureCanAccessAsync(long actor,long ticketId,CancellationToken ct){var ticket=await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==ticketId,ct)??throw new KeyNotFoundException("Ticket was not found.");await authorizer.EnsureCanAccessAsync(actor,ToTicket(ticket),ct);}
    private async Task EnsureCanMutateAsync(long actor,long ticketId,CancellationToken ct){var ticket=await db.Tickets.SingleOrDefaultAsync(x=>x.Id==ticketId,ct)??throw new KeyNotFoundException("Ticket was not found.");await authorizer.EnsureCanMutateAsync(actor,ToTicket(ticket),ct);}
    private static bool IsRetryablePostgresSerialization(Exception ex)
    {
        if (ex is DbUpdateException db && (db.InnerException is PostgresException { SqlState: "40001" or "40P01" } || db.InnerException?.Message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase) == true || db.InnerException?.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) == true))
            return true;
        if (ex is InvalidOperationException io && (io.InnerException is PostgresException { SqlState: "40001" or "40P01" } || io.InnerException?.Message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase) == true || io.InnerException?.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) == true))
            return true;
        return ex.InnerException is PostgresException { SqlState: "40001" or "40P01" }
            || ex.Message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase);
    }
    private static Ticket ToTicket(TicketRow ticket)=>Ticket.Restore(ticket.Id,ticket.Number,(TicketType)ticket.TypeId,(TicketStatus)ticket.StatusId,ticket.RequestedBy,ticket.CreatedBy,ticket.Subject,ticket.Description,ticket.ServiceId,ticket.CategoryId,ticket.RequestTypeId,ticket.PriorityId,ticket.CurrentDepartmentId,ticket.CurrentTeamId,ticket.CurrentAssignee,ticket.CreatedAt,ticket.ResolvedAt,ticket.ClosedAt,ticket.ApprovalRequired==1,ticket.Approved==1,ticket.EvidenceCount,ticket.LastExecutor,[]);
    private async Task<long> NextAsync(string sequence,CancellationToken ct){await db.Database.OpenConnectionAsync(ct);try{await using var c=db.Database.GetDbConnection().CreateCommand();c.CommandText=$"select nextval('\"{sequence}\"'::regclass)";return Convert.ToInt64(await c.ExecuteScalarAsync(ct));}finally{await db.Database.CloseConnectionAsync();}}
    private async Task AddAudit(string type,string id,string action,long actor,string? correlation,CancellationToken ct,string? newValue=null){await using var tx=await AuditTransaction.BeginIfNeededAsync(db,ct);await auditLocks.AcquireAsync(db,type,id,ct);var now=DateTimeOffset.UtcNow;var prev=await db.Audits.Where(x=>x.EntityType==type&&x.EntityId==id).OrderByDescending(x=>x.Id).Select(x=>x.Hash).FirstOrDefaultAsync(ct);var canonical=System.Text.Json.JsonSerializer.Serialize(new{entityType=type,entityId=id,action,actorUserId=actor,auditAtUtc=now.UtcDateTime,oldValue=(string?)null,newValue,reason=correlation,previousHash=prev});var hash=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();db.Audits.Add(new AuditRow{EntityType=type,EntityId=id,Action=action,ActorId=actor,At=now,NewValue=newValue,Reason=correlation,Hash=hash,PreviousHash=prev});await db.SaveChangesAsync(ct);await tx.CommitIfOwnedAsync(ct);}
    private static AttachmentVersion MapVersion(AttachmentVersionRow x)=>new(x.Id,x.AttachmentId,x.VersionNo,x.FileName,x.ContentType,x.FileSize??0,x.StorageProvider??"",x.StorageReference??"",x.Sha256Hash,x.Status,x.ChangeReason,x.UploadedAt,x.UploadedBy);
    private static Attachment MapAttachment(AttachmentRow x)=>new(x.Id,x.TicketId,x.WorkItemId,x.Type,x.FileName,x.ContentType,x.FileSize??0,x.StorageProvider??"",x.StorageReference??"",x.Sha256Hash,x.CurrentVersion,x.Active,x.UploadedAt,x.UploadedBy);
    private static Evidence MapEvidence(EvidenceRow x)=>new(x.Id,x.TicketId,x.WorkItemId,x.AttachmentId,x.Code,x.Type,x.Description,x.ControlReference,x.Status,x.ValidFrom,x.ReviewDueAt,x.ReviewedBy,x.ReviewedAt,x.ReviewComment,x.CreatedAt,x.CreatedBy,x.RetentionUntil,x.DisposalReason);
}
