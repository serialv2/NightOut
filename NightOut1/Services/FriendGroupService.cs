using Newtonsoft.Json;
using NightOut.Models;
using SkiaSharp;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class FriendGroupService(Client supabase, IAuthService auth, IFriendService friends, ICreditService credits, IBarService bars, INotificationService notifications) : IFriendGroupService
{
    private const string Bucket = "group-media";
    private const int MaxImageEdge = 1080;
    private const int JpegQuality = 80;
    private const long MaxVideoBytes = 80L * 1024L * 1024L;

    public async Task<List<FriendGroup>> GetMyGroupsAsync()
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me)) return [];
        try
        {
            var memberRows = await supabase.From<FriendGroupMember>()
                .Filter("user_id", Operator.Equals, me)
                .Get();

            var memberGroupIds = memberRows?.Models?.Select(m => m.GroupId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList() ?? [];

            var groups = new List<FriendGroup>();
            var owned = await supabase.From<FriendGroup>()
                .Filter("owner_id", Operator.Equals, me)
                .Order(g => g.CreatedAt, Ordering.Descending)
                .Get();
            if (owned?.Models != null) groups.AddRange(owned.Models);

            if (memberGroupIds.Count > 0)
            {
                var memberGroups = await supabase.From<FriendGroup>()
                    .Filter("id", Operator.In, memberGroupIds)
                    .Get();
                if (memberGroups?.Models != null) groups.AddRange(memberGroups.Models);
            }

            return groups.GroupBy(g => g.Id).Select(g => g.First()).OrderByDescending(g => g.UpdatedAt).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] GetMyGroups error: {ex.Message}");
            return [];
        }
    }

    public async Task<FriendGroup?> CreateGroupAsync(string name, string emoji = "🍻")
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            var group = new FriendGroup
            {
                OwnerId = me,
                Name = name.Trim(),
                Emoji = string.IsNullOrWhiteSpace(emoji) ? "🍻" : emoji.Trim()
            };
            var result = await supabase.From<FriendGroup>().Insert(group);
            var created = result?.Models?.FirstOrDefault() ?? group;

            if (!string.IsNullOrWhiteSpace(created.Id))
            {
                await AddCreatorAsMemberAsync(created.Id, me);
                await credits.AddMyCreditsByRuleAsync("group_created", created.Id, "friend_group", 20);
            }
            return created;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] CreateGroup error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateGroupPhotoAsync(string groupId, string photoUrl)
    {
        if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(photoUrl)) return false;
        try
        {
            await supabase.From<FriendGroup>()
                .Where(g => g.Id == groupId)
                .Set(g => g.PhotoUrl, photoUrl)
                .Update();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] UpdateGroupPhoto error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AddMemberAsync(string groupId, string userId)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(userId)) return false;
        try
        {
            var accepted = await friends.GetAcceptedFriendshipsAsync(me);
            var isFriend = accepted.Any(f => f.GetFriendId(me) == userId);
            if (!isFriend && userId != me) return false;

            var existing = await supabase.From<FriendGroupMember>()
                .Filter("group_id", Operator.Equals, groupId)
                .Filter("user_id", Operator.Equals, userId)
                .Get();
            if (existing?.Models?.Any() == true) return true;

            await supabase.From<FriendGroupMember>().Insert(new FriendGroupMember
            {
                GroupId = groupId,
                UserId = userId,
                AddedBy = me
            });
            await credits.AddMyCreditsByRuleAsync("group_member_added", groupId, "friend_group", 5);
            await notifications.PushAsync(userId, "group_member_added", me, groupId, "friend_group");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] AddMember error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RemoveMemberAsync(string groupId, string userId)
    {
        var me = auth.GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(me) ||
            string.IsNullOrWhiteSpace(groupId) ||
            string.IsNullOrWhiteSpace(userId))
            return false;

        try
        {
            var group = await supabase.From<FriendGroup>()
                .Where(g => g.Id == groupId)
                .Single();

            if (group == null || group.OwnerId != me)
                return false;

            await supabase.From<FriendGroupMember>()
                .Filter("group_id", Operator.Equals, groupId)
                .Filter("user_id", Operator.Equals, userId)
                .Delete();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] RemoveMember error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteGroupAsync(string groupId)
    {
        var me = auth.GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(groupId))
            return false;

        try
        {
            var group = await supabase.From<FriendGroup>()
                .Where(g => g.Id == groupId)
                .Single();

            if (group == null || group.OwnerId != me)
                return false;

            try
            {
                var outings = await supabase.From<FriendGroupOuting>()
                    .Filter("group_id", Operator.Equals, groupId)
                    .Get();

                var outingIds = outings?.Models?
                    .Select(o => o.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList() ?? [];

                if (outingIds.Count > 0)
                {
                    await supabase.From<FriendGroupOutingResponse>()
                        .Filter("outing_id", Operator.In, outingIds)
                        .Delete();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FriendGroupService] DeleteGroup outing responses warning: {ex.Message}");
            }

            await supabase.From<FriendGroupMember>()
                .Filter("group_id", Operator.Equals, groupId)
                .Delete();

            await supabase.From<FriendGroupMessage>()
                .Filter("group_id", Operator.Equals, groupId)
                .Delete();

            await supabase.From<FriendGroupOuting>()
                .Filter("group_id", Operator.Equals, groupId)
                .Delete();

            await supabase.From<FriendGroup>()
                .Where(g => g.Id == groupId)
                .Delete();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] DeleteGroup error: {ex.Message}");
            return false;
        }
    }

    public async Task<List<FriendGroupMember>> GetMembersAsync(string groupId)
    {
        try
        {
            var result = await supabase.From<FriendGroupMember>()
                .Filter("group_id", Operator.Equals, groupId)
                .Order(m => m.CreatedAt, Ordering.Ascending)
                .Get();
            var models = result?.Models ?? [];
            await AttachProfilesAsync(models, m => m.UserId, (m, p) => m.Profile = p);
            return models;
        }
        catch { return []; }
    }

    public async Task<List<FriendGroupMessage>> GetMessagesAsync(string groupId, int limit = 50)
    {
        try
        {
            var result = await supabase.From<FriendGroupMessage>()
                .Filter("group_id", Operator.Equals, groupId)
                .Order(m => m.CreatedAt, Ordering.Descending)
                .Limit(limit)
                .Get();
            var models = (result?.Models ?? []).OrderBy(m => m.CreatedAt).ToList();
            await AttachProfilesAsync(models, m => m.SenderId, (m, p) => m.Sender = p);
            return models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] GetMessages error: {ex.Message}");
            return [];
        }
    }

    public async Task<FriendGroupMessage?> SendTextMessageAsync(string groupId, string text)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(text)) return null;

        var message = await InsertGroupMessageRpcAsync(groupId, text.Trim(), null, "text");
        if (message != null)
        {
            await credits.AddMyCreditsByRuleAsync("group_message", groupId, "friend_group", 2);
            await NotifyGroupMembersAsync(groupId, "group_message", message.Id);
            return message;
        }

        // Secours si le script SQL RPC n'a pas encore été exécuté.
        try
        {
            var fallback = new FriendGroupMessage
            {
                GroupId = groupId,
                SenderId = me,
                MessageText = text.Trim(),
                MessageType = "text"
            };
            var result = await supabase.From<FriendGroupMessage>().Insert(fallback);
            await credits.AddMyCreditsByRuleAsync("group_message", groupId, "friend_group", 2);
            var createdFallback = result?.Models?.FirstOrDefault() ?? fallback;
            await NotifyGroupMembersAsync(groupId, "group_message", createdFallback.Id);
            return createdFallback;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] SendTextMessage fallback error: {ex}");
            return null;
        }
    }

    public async Task<FriendGroupMessage?> SendPhotoMessageAsync(string groupId, bool fromCamera)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(groupId)) return null;

        var url = await PickCompressAndUploadPhotoAsync($"{groupId}/{me}/{Guid.NewGuid():N}.jpg", fromCamera);
        if (string.IsNullOrWhiteSpace(url)) return null;

        var message = await InsertGroupMessageRpcAsync(groupId, null, url, "photo");
        if (message != null)
        {
            await credits.AddMyCreditsByRuleAsync("group_photo", groupId, "friend_group", 10);
            await NotifyGroupMembersAsync(groupId, "group_photo", message.Id);
            return message;
        }

        // Secours si le script SQL RPC n'a pas encore été exécuté.
        try
        {
            var fallback = new FriendGroupMessage
            {
                GroupId = groupId,
                SenderId = me,
                PhotoUrl = url,
                MessageType = "photo"
            };
            var result = await supabase.From<FriendGroupMessage>().Insert(fallback);
            await credits.AddMyCreditsByRuleAsync("group_photo", groupId, "friend_group", 10);
            var createdFallback = result?.Models?.FirstOrDefault() ?? fallback;
            await NotifyGroupMembersAsync(groupId, "group_photo", createdFallback.Id);
            return createdFallback;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] SendPhotoMessage fallback error: {ex}");
            return null;
        }
    }


    public async Task<FriendGroupMessage?> SendVideoMessageAsync(string groupId, bool fromCamera)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(groupId)) return null;

        var url = await PickAndUploadVideoAsync($"{groupId}/{me}/{Guid.NewGuid():N}.mp4", fromCamera);
        if (string.IsNullOrWhiteSpace(url)) return null;

        var message = await InsertGroupMessageRpcAsync(groupId, null, url, "video");
        if (message != null)
        {
            await credits.AddMyCreditsByRuleAsync("group_video", groupId, "friend_group", 15);
            await NotifyGroupMembersAsync(groupId, "group_video", message.Id);
            return message;
        }

        try
        {
            var fallback = new FriendGroupMessage
            {
                GroupId = groupId,
                SenderId = me,
                PhotoUrl = url,
                MessageType = "video"
            };
            var result = await supabase.From<FriendGroupMessage>().Insert(fallback);
            await credits.AddMyCreditsByRuleAsync("group_video", groupId, "friend_group", 15);
            var createdFallback = result?.Models?.FirstOrDefault() ?? fallback;
            await NotifyGroupMembersAsync(groupId, "group_video", createdFallback.Id);
            return createdFallback;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] SendVideoMessage fallback error: {ex}");
            return null;
        }
    }

    private async Task<FriendGroupMessage?> InsertGroupMessageRpcAsync(string groupId, string? text, string? photoUrl, string messageType)
    {
        try
        {
            var response = await supabase.Rpc("nightout_send_group_message", new
            {
                p_group_id = groupId,
                p_message_text = text,
                p_photo_url = photoUrl,
                p_message_type = messageType
            });

            if (string.IsNullOrWhiteSpace(response?.Content)) return null;
            var message = JsonConvert.DeserializeObject<FriendGroupMessage>(response.Content);
            if (message != null)
            {
                if (string.IsNullOrWhiteSpace(message.GroupId)) message.GroupId = groupId;
                if (string.IsNullOrWhiteSpace(message.SenderId)) message.SenderId = auth.GetCurrentUserId() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(message.MessageType)) message.MessageType = messageType;
                if (message.MessageText is null) message.MessageText = text;
                if (message.PhotoUrl is null) message.PhotoUrl = photoUrl;
            }
            return message;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] InsertGroupMessageRpc error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<FriendGroupOuting>> GetOutingsAsync(string groupId, int limit = 20)
    {
        try
        {
            // On ne veut plus afficher les anciennes soirées dans la page groupe.
            // On charge un peu plus large, puis on filtre côté app pour éviter les soucis
            // de conversion UTC/local avec Supabase/PostgREST.
            var nowUtc = DateTime.UtcNow;

            var outings = await supabase.From<FriendGroupOuting>()
                .Filter("group_id", Operator.Equals, groupId)
                .Order(o => o.PlannedAt, Ordering.Descending)
                .Limit(Math.Max(limit * 5, 50))
                .Get();

            var models = (outings?.Models ?? [])
                .Where(o => o.PlannedAt == default || o.PlannedAt.ToUniversalTime() >= nowUtc)
                .OrderBy(o => o.PlannedAt)
                .Take(limit)
                .ToList();

            foreach (var outing in models)
            {
                try { outing.Bar = await bars.GetBarByIdAsync(outing.BarId); } catch { }
                try
                {
                    var responses = await supabase.From<FriendGroupOutingResponse>()
                        .Filter("outing_id", Operator.Equals, outing.Id)
                        .Get();
                    outing.Responses = responses?.Models ?? [];
                }
                catch { outing.Responses = []; }
            }
            return models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] GetOutings error: {ex.Message}");
            return [];
        }
    }

    public async Task<FriendGroupOuting?> CreateOutingAsync(string groupId, string barId, string title, string? message, DateTime plannedAt)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(barId)) return null;

        var utcPlannedAt = plannedAt == default ? DateTime.UtcNow.AddHours(2) : plannedAt.ToUniversalTime();

        try
        {
            var response = await supabase.Rpc("nightout_create_group_outing", new
            {
                p_group_id = groupId,
                p_bar_id = barId,
                p_title = string.IsNullOrWhiteSpace(title) ? "Sortie entre amis" : title.Trim(),
                p_message = message,
                p_planned_at = utcPlannedAt
            });

            if (!string.IsNullOrWhiteSpace(response?.Content))
            {
                var created = JsonConvert.DeserializeObject<FriendGroupOuting>(response.Content);
                if (created != null)
                {
                    if (string.IsNullOrWhiteSpace(created.GroupId)) created.GroupId = groupId;
                    if (string.IsNullOrWhiteSpace(created.BarId)) created.BarId = barId;
                    if (string.IsNullOrWhiteSpace(created.CreatedBy)) created.CreatedBy = me;
                    await credits.AddMyCreditsByRuleAsync("group_outing_created", created.Id, "group_outing", 20);
                    await NotifyGroupMembersAsync(groupId, "group_event", created.Id);
                    await SendTextMessageAsync(groupId, $"🍻 Sortie proposée : {created.Title} ({created.PlannedLabel}).");
                    return created;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] CreateOuting RPC error: {ex.Message}");
        }

        try
        {
            var outing = new FriendGroupOuting
            {
                GroupId = groupId,
                CreatedBy = me,
                BarId = barId,
                Title = string.IsNullOrWhiteSpace(title) ? "Sortie entre amis" : title.Trim(),
                Message = message,
                PlannedAt = utcPlannedAt
            };
            var result = await supabase.From<FriendGroupOuting>().Insert(outing);
            var created = result?.Models?.FirstOrDefault() ?? outing;
            await credits.AddMyCreditsByRuleAsync("group_outing_created", created.Id, "group_outing", 20);
            await NotifyGroupMembersAsync(groupId, "group_event", created.Id);
            await SendTextMessageAsync(groupId, $"🍻 Sortie proposée : {created.Title} ({created.PlannedLabel}).");
            return created;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] CreateOuting fallback error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> RespondToOutingAsync(string outingId, string status)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(outingId)) return false;
        status = status is "yes" or "no" or "maybe" ? status : "maybe";

        try
        {
            await supabase.Rpc("nightout_respond_group_outing", new
            {
                p_outing_id = outingId,
                p_status = status
            });

            if (status == "yes") await credits.AddMyCreditsByRuleAsync("group_outing_yes", outingId, "group_outing", 15);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] RespondToOuting RPC error: {ex.Message}");
        }

        try
        {
            var existing = await supabase.From<FriendGroupOutingResponse>()
                .Filter("outing_id", Operator.Equals, outingId)
                .Filter("user_id", Operator.Equals, me)
                .Get();

            if (existing?.Models?.FirstOrDefault() is { } row)
            {
                await supabase.From<FriendGroupOutingResponse>()
                    .Where(r => r.Id == row.Id)
                    .Set(r => r.Status, status)
                    .Update();
            }
            else
            {
                await supabase.From<FriendGroupOutingResponse>().Insert(new FriendGroupOutingResponse
                {
                    OutingId = outingId,
                    UserId = me,
                    Status = status
                });
            }

            if (status == "yes") await credits.AddMyCreditsByRuleAsync("group_outing_yes", outingId, "group_outing", 15);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] RespondToOuting fallback error: {ex.Message}");
            return false;
        }
    }

    private async Task NotifyGroupMembersAsync(string groupId, string type, string? entityId)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(groupId)) return;

        try
        {
            var members = await supabase.From<FriendGroupMember>()
                .Filter("group_id", Operator.Equals, groupId)
                .Get();

            var memberIds = members?.Models?
                .Select(m => m.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id) && id != me)
                .Distinct()
                .ToList() ?? [];

            foreach (var userId in memberIds)
                await notifications.PushAsync(userId, type, me, entityId ?? groupId, type == "group_event" ? "group_outing" : "friend_group");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] NotifyGroupMembers error: {ex.Message}");
        }
    }

    private async Task<string?> PickCompressAndUploadPhotoAsync(string path, bool fromCamera)
    {
        try
        {
            FileResult? file;
            if (fromCamera)
            {
                if (!MediaPicker.Default.IsCaptureSupported) return null;
                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted) return null;
                file = await MediaPicker.Default.CapturePhotoAsync();
            }
            else
            {
                file = await MediaPicker.Default.PickPhotoAsync();
            }
            if (file == null) return null;

            byte[] compressed;
            using (var src = await file.OpenReadAsync())
            using (var ms = new MemoryStream())
            {
                await src.CopyToAsync(ms);
                compressed = CompressImage(ms.ToArray());
            }
            await supabase.Storage.From(Bucket).Upload(compressed, path, new Supabase.Storage.FileOptions
            {
                ContentType = "image/jpeg",
                Upsert = false
            });
            return supabase.Storage.From(Bucket).GetPublicUrl(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] Upload photo error: {ex.Message}");
            return null;
        }
    }


    private async Task<string?> PickAndUploadVideoAsync(string path, bool fromCamera)
    {
        try
        {
            FileResult? file;
            if (fromCamera)
            {
                if (!MediaPicker.Default.IsCaptureSupported) return null;
                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted) return null;
                file = await MediaPicker.Default.CaptureVideoAsync();
            }
            else
            {
                file = await MediaPicker.Default.PickVideoAsync();
            }

            if (file == null) return null;

            await using var src = await file.OpenReadAsync();
            if (src.CanSeek && src.Length > MaxVideoBytes)
            {
                System.Diagnostics.Debug.WriteLine("[FriendGroupService] Video trop lourde pour l'envoi groupe.");
                return null;
            }

            using var ms = new MemoryStream();
            await src.CopyToAsync(ms);
            var bytes = ms.ToArray();
            if (bytes.Length > MaxVideoBytes) return null;

            await supabase.Storage.From(Bucket).Upload(bytes, path, new Supabase.Storage.FileOptions
            {
                ContentType = "video/mp4",
                Upsert = false
            });

            return supabase.Storage.From(Bucket).GetPublicUrl(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] Upload video error: {ex.Message}");
            return null;
        }
    }

    private async Task AttachProfilesAsync<T>(List<T> items, Func<T, string> idSelector, Action<T, Profile> assign)
    {
        var ids = items.Select(idSelector).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0) return;
        try
        {
            var profiles = await supabase.From<Profile>().Filter("id", Operator.In, ids).Get();
            var map = profiles?.Models?.ToDictionary(p => p.Id) ?? [];
            foreach (var item in items)
                if (map.TryGetValue(idSelector(item), out var profile)) assign(item, profile);
        }
        catch { }
    }

    private async Task AddCreatorAsMemberAsync(string groupId, string userId)
    {
        if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(userId))
            return;

        try
        {
            var existing = await supabase.From<FriendGroupMember>()
                .Filter("group_id", Operator.Equals, groupId)
                .Filter("user_id", Operator.Equals, userId)
                .Get();

            if (existing?.Models?.Any() == true)
            {
                System.Diagnostics.Debug.WriteLine($"[FriendGroupService] Créateur déjà membre du groupe {groupId}");
                return;
            }

            await supabase.From<FriendGroupMember>().Insert(new FriendGroupMember
            {
                GroupId = groupId,
                UserId = userId,
                AddedBy = userId
            });

            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] Créateur ajouté comme membre au groupe {groupId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendGroupService] AddCreatorAsMemberAsync error: {ex.Message}");
        }
    }

    private static byte[] CompressImage(byte[] input, int maxEdge = MaxImageEdge, int quality = JpegQuality)
    {
        try
        {
            var origin = GetEncodedOrigin(input);
            using var original = SKBitmap.Decode(input);
            if (original is null) return input;

            var oriented = ApplyEncodedOrientation(original, origin);
            try
            {
                int w = oriented.Width, h = oriented.Height;
                int longEdge = Math.Max(w, h);

                SKBitmap toEncode = oriented;
                SKBitmap? resized = null;

                if (longEdge > maxEdge)
                {
                    float scale = (float)maxEdge / longEdge;
                    var info = new SKImageInfo(Math.Max(1, (int)(w * scale)), Math.Max(1, (int)(h * scale)));
                    resized = oriented.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                    if (resized != null) toEncode = resized;
                }

                using var image = SKImage.FromBitmap(toEncode);
                using var data  = image.Encode(SKEncodedImageFormat.Jpeg, quality);
                resized?.Dispose();
                return data.ToArray();
            }
            finally
            {
                if (!ReferenceEquals(oriented, original))
                    oriented.Dispose();
            }
        }
        catch
        {
            return input; // en cas de souci, on uploade l'original
        }
    }

    private static SKEncodedOrigin GetEncodedOrigin(byte[] input)
    {
        try
        {
            using var data = SKData.CreateCopy(input);
            using var codec = SKCodec.Create(data);
            return codec?.EncodedOrigin ?? SKEncodedOrigin.TopLeft;
        }
        catch
        {
            return SKEncodedOrigin.TopLeft;
        }
    }

    private static SKBitmap ApplyEncodedOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return source;

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var outputWidth = swap ? source.Height : source.Width;
        var outputHeight = swap ? source.Width : source.Height;
        var rotated = new SKBitmap(outputWidth, outputHeight, source.ColorType, source.AlphaType);

        using var canvas = new SKCanvas(rotated);
        canvas.Clear(SKColors.Transparent);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(outputWidth, 0);
                canvas.Scale(-1, 1);
                break;

            case SKEncodedOrigin.BottomRight:
                canvas.Translate(outputWidth, outputHeight);
                canvas.RotateDegrees(180);
                break;

            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, outputHeight);
                canvas.Scale(1, -1);
                break;

            case SKEncodedOrigin.RightTop:
                canvas.Translate(outputWidth, 0);
                canvas.RotateDegrees(90);
                break;

            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, outputHeight);
                canvas.RotateDegrees(270);
                break;

            case SKEncodedOrigin.LeftTop:
                canvas.Translate(0, outputHeight);
                canvas.RotateDegrees(270);
                canvas.Scale(-1, 1);
                break;

            case SKEncodedOrigin.RightBottom:
                canvas.Translate(outputWidth, 0);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return rotated;
    }
}
