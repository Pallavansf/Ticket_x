using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlAttachmentEvidenceTests
{
    [Fact]
    public async Task Attachment_storage_versioning_checksum_download_and_evidence_review_work_against_postgresql()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var root = Path.Combine(Path.GetTempPath(), "pa-tickets-slice2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await using var provider = BuildProvider(root);
        var ticketId = 0L;
        var attachmentId = 0L;
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                ticketId = (await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Attachment {Guid.NewGuid():N}", "slice 2", 1001, 1001), CancellationToken.None)).Id;
                var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
                await using var first = new MemoryStream("first bytes"u8.ToArray());
                var firstResult = await service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","secret.txt","text/plain","INTERNAL","first",first,"slice2-upload-1","slice2"),1001,CancellationToken.None);
                attachmentId = firstResult.Attachment.Id;
                Assert.Equal("secret.txt", firstResult.Version.FileName);
                Assert.Equal("Local", firstResult.Version.StorageProvider);
                Assert.DoesNotContain(root, firstResult.Version.StorageObjectKey, StringComparison.OrdinalIgnoreCase);
                Assert.Matches("^[0-9a-f]{64}$", firstResult.Version.Sha256Hash);
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","duplicate.txt","text/plain","INTERNAL","duplicate",new MemoryStream("duplicate"u8.ToArray()),"slice2-upload-1","slice2"),1001,CancellationToken.None));
                await using var downloaded = (await service.DownloadAsync(attachmentId,1,1001,CancellationToken.None)).Content;
                using var reader = new StreamReader(downloaded);
                Assert.Equal("first bytes", await reader.ReadToEndAsync());

                await using var second = new MemoryStream("second bytes"u8.ToArray());
                var secondResult = await service.CreateVersionAsync(attachmentId,"second.txt","text/plain",second,"replace","slice2-upload-2","slice2",1001,CancellationToken.None);
                Assert.Equal(2, secondResult.Version.VersionNo);
                var versions = await service.VersionsAsync(attachmentId,1001,CancellationToken.None);
                Assert.Equal(["SUPERSEDED","CURRENT"], versions.Select(x => x.Status));
                var evidence = await service.CreateEvidenceAsync(new CreateEvidenceCommand(ticketId,null,attachmentId,"EV-SLICE2","TEST_RESULT","Attachment evidence","CTRL-2",DateTimeOffset.UtcNow.AddDays(30),DateTimeOffset.UtcNow.AddDays(90),"slice2-evidence-1","slice2"),1001,CancellationToken.None);
                Assert.Equal("CREATED", evidence.Status);
                var reviewed = await service.ReviewEvidenceAsync(evidence.Id,new ReviewEvidenceCommand("UNDER_REVIEW","needs review"),1001,CancellationToken.None);
                Assert.Equal(1001, reviewed.ReviewedBy);
                Assert.Equal("UNDER_REVIEW", reviewed.Status);
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateVersionAsync(attachmentId,"third.txt","text/plain",new MemoryStream("third"u8.ToArray()),"retry", "slice2-upload-2","slice2",1001,CancellationToken.None));
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var version = await db.AttachmentVersions.SingleAsync(x => x.AttachmentId == attachmentId && x.VersionNo == 2);
                Assert.Equal(1, await db.AttachmentVersions.CountAsync(x => x.AttachmentId == attachmentId && x.Status == "CURRENT"));
                var originalHash = version.Sha256Hash;
                version.Sha256Hash = new string('0', 64);
                await db.SaveChangesAsync();
                var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadAsync(attachmentId, 2, 1001, CancellationToken.None));
                version.Sha256Hash = originalHash;
                await db.SaveChangesAsync();
                Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "ATTACHMENT" && x.EntityId == attachmentId.ToString() && x.Action == "ATTACHMENT_CREATED"));
                Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "ATTACHMENT_CREATED"));
                Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "ATTACHMENT_VERSION_CREATED"));
                Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "EVIDENCE_CREATED"));
                Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "EVIDENCE_REVIEWED"));
                var existingVersionId = await db.AttachmentVersions.AsNoTracking()
                    .Where(x => x.AttachmentId == attachmentId && x.VersionNo == 1)
                    .Select(x => x.Id)
                    .SingleAsync();
                await Assert.ThrowsAsync<DbUpdateException>(async () =>
                {
                    db.AttachmentVersions.Add(new AttachmentVersionRow
                    {
                        Id = existingVersionId,
                        AttachmentId = attachmentId,
                        VersionNo = 99,
                        FileName = "bad",
                        Sha256Hash = "bad",
                        Status = "CURRENT",
                        UploadedAt = DateTimeOffset.UtcNow,
                        UploadedBy = 1001
                    });
                    await db.SaveChangesAsync();
                });
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            if (ticketId != 0)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVIDENCE"" where ""N_TICKET_ID"" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ATTACHMENT_VERSION"" where ""N_ATTACHMENT_ID"" = {attachmentId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ATTACHMENT"" where ""N_TICKET_ID"" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {ticketId}");
            }
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Attachment_content_is_detected_scanned_and_stored_only_after_clean_result()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var root=Path.Combine(Path.GetTempPath(),"pa-tickets-inspection-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var scanner=new ProgrammableContentScanner();
        await using var provider=BuildProvider(root,scanner:scanner);
        var ticketId=0L;
        try
        {
            await using var scope=provider.CreateAsyncScope();
            ticketId=(await scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>().CreateAsync(new CreateTicketCommand(TicketType.Request,"Content inspection",null,1001,1001),CancellationToken.None)).Id;
            var service=scope.ServiceProvider.GetRequiredService<IAttachmentService>();
            var pdf=System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\nsafe document\n%%EOF");
            var accepted=await service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","invoice.pdf","application/pdf",null,null,new MemoryStream(pdf),"scan-pdf","scan"),1001,CancellationToken.None);
            Assert.Equal("application/pdf",accepted.Version.ContentType);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf)).ToLowerInvariant(),accepted.Version.Sha256Hash);
            Assert.Equal(ContentScanStatus.Clean,scanner.LastResult);
            Assert.True(await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Audits.Where(x=>x.EntityId==accepted.Attachment.Id.ToString()).AnyAsync(x=>x.NewValue!=null&&x.NewValue.Contains("declaredMimeType")));

            var jpeg=new byte[]{0xff,0xd8,0xff,0xe0,0x00,0x10,0x4a,0x46,0x49,0x46};
            var acceptedJpeg=await service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","photo.jpg","image/jpeg",null,null,new MemoryStream(jpeg),"scan-jpeg","scan"),1001,CancellationToken.None);
            Assert.Equal("image/jpeg",acceptedJpeg.Version.ContentType);
            var scannerCalls=scanner.Calls;
            await Assert.ThrowsAsync<ContentRejectedException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","renamed.pdf","application/pdf",null,null,new MemoryStream(jpeg),"scan-mismatch","scan"),1001,CancellationToken.None));
            await Assert.ThrowsAsync<ContentRejectedException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","renamed.jpg","image/jpeg",null,null,new MemoryStream(pdf),"scan-mismatch-2","scan"),1001,CancellationToken.None));
            await Assert.ThrowsAsync<ContentRejectedException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","malware.pdf","application/pdf",null,null,new MemoryStream(System.Text.Encoding.ASCII.GetBytes("MZ executable")),"scan-exe","scan"),1001,CancellationToken.None));
            await Assert.ThrowsAsync<ContentRejectedException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","unknown.pdf","application/pdf",null,null,new MemoryStream(new byte[]{0,0,1,2,3,4,5,6}),"scan-unknown","scan"),1001,CancellationToken.None));
            await Assert.ThrowsAsync<ContentRejectedException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","archive.zip","application/zip",null,null,new MemoryStream(new byte[]{0x50,0x4b,0x03,0x04,0x00,0x00,0x00,0x00}),"scan-archive","scan"),1001,CancellationToken.None));
            Assert.Equal(scannerCalls,scanner.Calls);

            scanner.Status=ContentScanStatus.Rejected;
            await Assert.ThrowsAsync<ContentRejectedException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","rejected.pdf","application/pdf",null,null,new MemoryStream(pdf),"scan-rejected","scan"),1001,CancellationToken.None));
            await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.DownloadAsync(long.MaxValue,1,1001,CancellationToken.None));
            foreach(var status in new[]{ContentScanStatus.Unavailable,ContentScanStatus.Error,ContentScanStatus.Timeout})
            {
                scanner.Status=status;
                await Assert.ThrowsAsync<ContentScannerUnavailableException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE",$"{status}.pdf","application/pdf",null,null,new MemoryStream(pdf),$"scan-{status}","scan"),1001,CancellationToken.None));
            }
            scanner.Throw=true;
            await Assert.ThrowsAsync<ContentScannerUnavailableException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","scanner-error.pdf","application/pdf",null,null,new MemoryStream(pdf),"scan-throw","scan"),1001,CancellationToken.None));

            var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            Assert.Equal(2,await db.Attachments.CountAsync(x=>x.TicketId==ticketId));
            Assert.Equal(2,Directory.GetFiles(root,"*",SearchOption.AllDirectories).Length);
            await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.CreateEvidenceAsync(new CreateEvidenceCommand(ticketId,null,long.MaxValue,"EV-REJECTED","TEST_RESULT",null,null,null,null,"evidence-rejected","scan"),1001,CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","../traversal.pdf","application/pdf",null,null,new MemoryStream(pdf),"scan-traversal","scan"),1001,CancellationToken.None));
            await Assert.ThrowsAsync<ContentRejectedException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","double.pdf.exe","application/pdf",null,null,new MemoryStream(pdf),"scan-double","scan"),1001,CancellationToken.None));
        }
        finally
        {
            await CleanupAsync(provider,ticketId,0);
            Directory.Delete(root,true);
        }
    }

    [Fact]
    public async Task Upload_fails_closed_when_scanner_is_not_configured_and_before_storage_for_oversize_content()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var root=Path.Combine(Path.GetTempPath(),"pa-tickets-unavailable-scan-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await using var provider=BuildProvider(root,scanner:new UnconfiguredContentScanner(),maxFileSizeBytes:4);
        var ticketId=0L;
        try
        {
            await using var scope=provider.CreateAsyncScope();
            ticketId=(await scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>().CreateAsync(new CreateTicketCommand(TicketType.Request,"No scanner",null,1001,1001),CancellationToken.None)).Id;
            var service=scope.ServiceProvider.GetRequiredService<IAttachmentService>();
            await Assert.ThrowsAsync<AttachmentTooLargeException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","large.txt","text/plain",null,null,new MemoryStream("12345"u8.ToArray()),"oversized","scan"),1001,CancellationToken.None));
            await Assert.ThrowsAsync<ContentScannerUnavailableException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","safe.txt","text/plain",null,null,new MemoryStream("safe"u8.ToArray()),"no-scanner","scan"),1001,CancellationToken.None));
            Assert.Empty(Directory.GetFiles(root,"*",SearchOption.AllDirectories));
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Attachments.Where(x=>x.TicketId==ticketId).ToListAsync());
        }
        finally
        {
            await CleanupAsync(provider,ticketId,0);
            Directory.Delete(root,true);
        }
    }

    [Fact]
    public async Task Local_storage_rejects_traversal_absolute_paths_and_oversized_streams()
    {
        var root = Path.Combine(Path.GetTempPath(), "pa-tickets-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Storage:RootPath"] = root }).Build();
            var storage = new PA.Tickets.Infrastructure.Foundations.LocalFileStorage(config);
            await Assert.ThrowsAsync<ArgumentException>(() => storage.PutAsync("../escape","x.txt","text/plain",new MemoryStream([1]),1024,CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync(Path.GetFullPath("escape"),CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.PutAsync("%2e%2e/escape","x.txt","text/plain",new MemoryStream([1]),1024,CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.PutAsync("/tmp/escape","x.txt","text/plain",new MemoryStream([1]),1024,CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.PutAsync("C:\\temp\\escape","x.txt","text/plain",new MemoryStream([1]),1024,CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.PutAsync("attachments/x/1","x.txt","text/plain",new MemoryStream(new byte[5]),4,CancellationToken.None));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Concurrent_version_creation_keeps_one_current_version_and_cleans_losing_storage()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var root = Path.Combine(Path.GetTempPath(), "pa-tickets-race-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await using var provider = BuildProvider(root);
        long ticketId = 0;
        long attachmentId = 0;
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                ticketId = (await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Attachment race {Guid.NewGuid():N}", "race", 1001, 1001), CancellationToken.None)).Id;
                var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
                attachmentId = (await service.CreateAsync(new CreateAttachmentCommand(ticketId, null, "EVIDENCE", "original.txt", "text/plain", "INTERNAL", null, new MemoryStream("original"u8.ToArray()), "race-original", "race"), 1001, CancellationToken.None)).Attachment.Id;
            }

            var attempts = Enumerable.Range(1, 2).Select(async number =>
            {
                await using var scope = provider.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
                try
                {
                    var result = await service.CreateVersionAsync(attachmentId, $"version-{number}.txt", "text/plain", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"version {number}")), "race", $"race-version-{number}", "race", 1001, CancellationToken.None);
                    return result.Version.VersionNo;
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException($"Concurrent version {number} failed: {exception.Message}", exception);
                }
            }).ToArray();
            var results = await Task.WhenAll(attempts);
            Assert.Equal([2, 3], results.OrderBy(x => x));

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                Assert.Equal(3, await db.AttachmentVersions.CountAsync(x => x.AttachmentId == attachmentId));
                Assert.Equal(1, await db.AttachmentVersions.CountAsync(x => x.AttachmentId == attachmentId && x.Status == "CURRENT"));
                Assert.Equal("original", await ReadStoredAsync(scope.ServiceProvider, attachmentId, 1));
            }
            Assert.Equal(3, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            if (ticketId != 0)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_ATTACHMENT_VERSION\" where \"N_ATTACHMENT_ID\" = {attachmentId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_ATTACHMENT\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
            }
            Directory.Delete(root, true);
        }
    }

    private static async Task<string> ReadStoredAsync(IServiceProvider services,long attachmentId,int version)
    {
        var service = services.GetRequiredService<IAttachmentService>();
        await using var stream = (await service.DownloadAsync(attachmentId, version, 1001, CancellationToken.None)).Content;
        return await new StreamReader(stream).ReadToEndAsync();
    }

    [Fact]
    public async Task Attachment_and_evidence_access_uses_ticket_authorization_for_metadata_versions_download_and_review()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var root = Path.Combine(Path.GetTempPath(), "pa-tickets-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var scanner=new ProgrammableContentScanner();
        await using var provider = BuildProvider(root, scanner: scanner);
        long ticketId = 0;
        long otherTicketId = 0;
        long attachmentId = 0;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            ticketId = (await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Attachment auth {Guid.NewGuid():N}", "auth", 1001, 1001), CancellationToken.None)).Id;
            otherTicketId = (await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Attachment auth other {Guid.NewGuid():N}", "auth", 1001, 1001), CancellationToken.None)).Id;
            var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
            var created = await service.CreateAsync(new CreateAttachmentCommand(ticketId, null, "EVIDENCE", "auth.txt", "text/plain", "INTERNAL", null, new MemoryStream("auth"u8.ToArray()), "auth-upload", "auth"), 1001, CancellationToken.None);
            attachmentId = created.Attachment.Id;
            var scannerCalls= scanner.Calls;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,null,"EVIDENCE","denied.txt","text/plain",null,null,new MemoryStream("denied"u8.ToArray()),"auth-denied","auth"),999999,CancellationToken.None));
            Assert.Equal(scannerCalls,scanner.Calls);

            var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            db.WorkItems.Add(new WorkItemRow{TicketId=otherTicketId,Number=$"WI-{Guid.NewGuid():N}",Status="PENDING",CreatedAt=DateTimeOffset.UtcNow,CreatedBy=1001});
            await db.SaveChangesAsync();
            var foreignWorkItemId=await db.WorkItems.Where(x=>x.TicketId==otherTicketId).Select(x=>x.Id).SingleAsync();
            await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.CreateAsync(new CreateAttachmentCommand(ticketId,foreignWorkItemId,"EVIDENCE","foreign-work-item.txt","text/plain",null,null,new MemoryStream("foreign"u8.ToArray()),"auth-foreign-work-item","auth"),1001,CancellationToken.None));
            Assert.Equal(scannerCalls,scanner.Calls);
            var evidence = await service.CreateEvidenceAsync(new CreateEvidenceCommand(ticketId, null, attachmentId, "EV-AUTH", "TEST_RESULT", null, null, null, null, "auth-evidence", "auth"), 1001, CancellationToken.None);

            Assert.Equal(attachmentId, (await service.GetAsync(attachmentId, 1001, CancellationToken.None)).Id);
            Assert.Single(await service.VersionsAsync(attachmentId, 1001, CancellationToken.None));
            await using var authorizedDownload = (await service.DownloadAsync(attachmentId, 1, 1001, CancellationToken.None)).Content;
            Assert.Equal("auth", await new StreamReader(authorizedDownload).ReadToEndAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(attachmentId, 999999, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.VersionsAsync(attachmentId, 999999, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DownloadAsync(attachmentId, 1, 999999, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReviewEvidenceAsync(evidence.Id, new ReviewEvidenceCommand("UNDER_REVIEW", "denied"), 999999, CancellationToken.None));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(attachmentId + 999999, 1001, CancellationToken.None));
        }
        finally
        {
            await CleanupAsync(provider,ticketId,attachmentId);
            await CleanupAsync(provider,otherTicketId,0);
            Directory.Delete(root, true);
        }
    }

    private static ServiceProvider BuildProvider(string root,bool restricted=false,IContentScanner? scanner=null,long? maxFileSizeBytes=null){var settings=new Dictionary<string,string?> { ["Database:Provider"]="PostgreSQL",["Storage:Provider"]="Local",["Storage:RootPath"]=root };if(maxFileSizeBytes.HasValue)settings["Storage:MaxFileSizeBytes"]=maxFileSizeBytes.Value.ToString();var config=new ConfigurationBuilder().AddInMemoryCollection(settings).Build();var services=new ServiceCollection();services.AddSingleton<IConfiguration>(config);services.AddApplication();services.AddInfrastructure(config);services.AddScoped<IContentScanner>(_=>scanner??new DeterministicContentScanner());if(restricted)services.AddScoped<ITicketAccessAuthorizer,TestTicketAccessAuthorizer>();return services.BuildServiceProvider(validateScopes:true);}

    private static async Task CleanupAsync(ServiceProvider provider,long ticketId,long attachmentId)
    {
        if(ticketId==0)return;
        await using var scope=provider.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVIDENCE\" where \"N_TICKET_ID\" = {ticketId}");
        if(attachmentId!=0)await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_ATTACHMENT_VERSION\" where \"N_ATTACHMENT_ID\" = {attachmentId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_ATTACHMENT_VERSION\" where \"N_ATTACHMENT_ID\" in (select \"N_ATTACHMENT_ID\" from \"PA_TICKETS_ATTACHMENT\" where \"N_TICKET_ID\" = {ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_ATTACHMENT\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_WORK_ITEM\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
    }

    private sealed class ProgrammableContentScanner:IContentScanner
    {
        public ContentScanStatus Status{get;set;}=ContentScanStatus.Clean;
        public bool Throw{get;set;}
        public int Calls{get;private set;}
        public ContentScanStatus? LastResult{get;private set;}
        public Task<ContentScanResult> ScanAsync(Stream content,CancellationToken ct)
        {
            Calls++;
            if(Throw)throw new InvalidOperationException("scanner internal error");
            LastResult=Status;
            return Task.FromResult(new ContentScanResult(Status,"test-scanner-v1"));
        }
    }

    private sealed class TestTicketAccessAuthorizer : ITicketAccessAuthorizer
    {
        public Task EnsureCanMutateAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId);
        public Task EnsureCanAccessAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId);
        public Task EnsureCanSearchAsync(long actorUserId, CancellationToken ct) => Ensure(actorUserId);
        private static Task Ensure(long actorUserId) => actorUserId == 1001 ? Task.CompletedTask : throw new UnauthorizedAccessException("Ticket access denied.");
    }
}
