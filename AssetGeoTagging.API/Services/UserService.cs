using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssetGeoTagging.DAL;
using AssetGeoTagging.Model;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Olameter.Sdi.Assetgeotagging.V1;

// Aliases to avoid name collisions between Model and Proto types
using UserModel = AssetGeoTagging.Model.User;
using UserGroupModel = AssetGeoTagging.Model.UserGroup;
using UserRoleModel = AssetGeoTagging.Model.UserRole;
using UserDto = Olameter.Sdi.Assetgeotagging.V1.User;
using UserGroupDto = Olameter.Sdi.Assetgeotagging.V1.UserGroup;
using UserRoleDto = Olameter.Sdi.Assetgeotagging.V1.UserRole;

namespace AssetGeoTagging.API.Services
{
    public class UserService : Olameter.Sdi.Assetgeotagging.V1.UserService.UserServiceBase
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        private static bool ParseStatusStringToBool(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var normalized = status.Trim();
            return normalized.Equals("active", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("1");
        }

        private static string StatusBoolToString(bool status)
        {
            return status ? "Active" : "Inactive";
        }

        private static string GenerateName(string prefix)
        {
            return (prefix ?? string.Empty) + Guid.NewGuid().ToString("N");
        }

        private static UserDto MapToUserDto(UserModel model)
        {
            var dto = new UserDto
            {
                Name = model.Name,
                UserName = model.UserName ?? string.Empty,
                Email = model.Eamil ?? string.Empty,
                PhoneNumber = model.PhoneNumber ?? string.Empty,
                Title = model.Title ?? string.Empty,
                Status = ParseStatusStringToBool(model.Status),
                UpdatedBy = model.UpdatedBy ?? string.Empty
            };

            if (model.UserRoles != null)
            {
                foreach (var role in model.UserRoles)
                {
                    dto.UserRoles.Add(new UserRoleDto
                    {
                        Name = role.Name,
                        RoleName = role.UserRoleName ?? string.Empty,
                        Description = role.Description ?? string.Empty
                    });
                }
            }

            if (model.UserGroups != null)
            {
                foreach (var group in model.UserGroups)
                {
                    dto.UserGroups.Add(new UserGroupDto
                    {
                        Name = group.Name,
                        UserGroupName = group.UserGroupName ?? string.Empty,
                        Description = group.Description ?? string.Empty,
                        Status = ParseStatusStringToBool(group.Status),
                        UpdatedBy = group.UpdatedBy ?? string.Empty
                    });
                }
            }

            return dto;
        }

        private static void MapUserDtoOntoEntity(UserDto dto, UserModel entity)
        {
            // Preserve entity.Name unless creating new
            entity.UserName = dto.UserName ?? entity.UserName;
            entity.Eamil = dto.Email ?? entity.Eamil;
            entity.PhoneNumber = dto.PhoneNumber ?? entity.PhoneNumber;
            entity.Title = dto.Title ?? entity.Title;
            entity.Status = StatusBoolToString(dto.Status);
            entity.UpdatedBy = dto.UpdatedBy ?? entity.UpdatedBy;
            entity.UpdatedOn = DateTime.UtcNow;
        }

        private async Task ReplaceUserGroupsAsync(UserModel user, IEnumerable<UserGroupDto> groups)
        {
            // Ensure collections are loaded
            if (user.UserGroups == null)
            {
                user.UserGroups = new List<UserGroupModel>();
            }

            var incomingByName = (groups ?? Enumerable.Empty<UserGroupDto>())
                .Where(g => !string.IsNullOrWhiteSpace(g.Name))
                .ToDictionary(g => g.Name, g => g);

            // Remove groups not present anymore
            var toRemove = user.UserGroups.Where(g => !incomingByName.ContainsKey(g.Name)).ToList();
            foreach (var g in toRemove)
            {
                user.UserGroups.Remove(g);
            }

            // Upsert existing and add new
            foreach (var kv in incomingByName)
            {
                var incoming = kv.Value;
                var existing = user.UserGroups.FirstOrDefault(g => g.Name == incoming.Name);
                if (existing == null)
                {
                    // Try to find existing group in DB by name to attach
                    existing = await _context.Set<UserGroupModel>().FirstOrDefaultAsync(g => g.Name == incoming.Name);
                    if (existing == null)
                    {
                        existing = new UserGroupModel
                        {
                            Name = string.IsNullOrWhiteSpace(incoming.Name) ? GenerateName(new UserGroupModel().DefaultResourceIdentifier) : incoming.Name,
                            CreatedOn = DateTime.UtcNow
                        };
                    }

                    user.UserGroups.Add(existing);
                }

                existing.UserGroupName = incoming.UserGroupName ?? existing.UserGroupName;
                existing.Description = incoming.Description ?? existing.Description;
                existing.Status = StatusBoolToString(incoming.Status);
                existing.UpdatedBy = incoming.UpdatedBy ?? existing.UpdatedBy;
                existing.UpdatedOn = DateTime.UtcNow;
                existing.User = user;
            }
        }

        private async Task ReplaceUserRolesAsync(UserModel user, IEnumerable<UserRoleDto> roles)
        {
            if (user.UserRoles == null)
            {
                user.UserRoles = new List<UserRoleModel>();
            }

            var incomingByName = (roles ?? Enumerable.Empty<UserRoleDto>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .ToDictionary(r => r.Name, r => r);

            var toRemove = user.UserRoles.Where(r => !incomingByName.ContainsKey(r.Name)).ToList();
            foreach (var r in toRemove)
            {
                user.UserRoles.Remove(r);
            }

            foreach (var kv in incomingByName)
            {
                var incoming = kv.Value;
                var existing = user.UserRoles.FirstOrDefault(r => r.Name == incoming.Name);
                if (existing == null)
                {
                    existing = await _context.Set<UserRoleModel>().FirstOrDefaultAsync(r => r.Name == incoming.Name);
                    if (existing == null)
                    {
                        existing = new UserRoleModel
                        {
                            Name = string.IsNullOrWhiteSpace(incoming.Name) ? GenerateName(new UserRoleModel().DefaultResourceIdentifier) : incoming.Name
                        };
                    }
                    user.UserRoles.Add(existing);
                }

                existing.UserRoleName = incoming.RoleName ?? existing.UserRoleName;
                existing.Description = incoming.Description ?? existing.Description;
                existing.User = user;
            }
        }

        public override async Task<ListUsersResponse> ListUsers(ListUsersRequest request, ServerCallContext context)
        {
            var page = request.Page ?? new PageRequest { Skip = 0, Take = 50 };
            if (page.Take <= 0) page.Take = 50;
            if (page.Skip < 0) page.Skip = 0;

            var baseQuery = _context.Set<UserModel>().AsNoTracking();
            var total = await baseQuery.CountAsync();

            var users = await baseQuery
                .Include(u => u.UserGroups)
                .Include(u => u.UserRoles)
                .OrderBy(u => u.CreatedOn)
                .Skip(page.Skip)
                .Take(page.Take)
                .ToListAsync();

            var response = new ListUsersResponse
            {
                Page = new PageResponse { Total = total }
            };
            response.Users.AddRange(users.Select(MapToUserDto));
            return response;
        }

        public override async Task<UserDto> GetUser(GetUserRequest request, ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "name is required"));
            }

            var user = await _context.Set<UserModel>()
                .Include(u => u.UserGroups)
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Name == request.Name);

            if (user == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"User '{request.Name}' not found"));
            }

            return MapToUserDto(user);
        }

        public override async Task<UserDto> SaveUser(SaveUserRequest request, ServerCallContext context)
        {
            if (request.User == null)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "user is required"));
            }

            var incoming = request.User;
            bool isCreate = string.IsNullOrWhiteSpace(incoming.Name);

            UserModel entity;
            if (isCreate)
            {
                entity = new UserModel
                {
                    Name = GenerateName(new UserModel().DefaultResourceIdentifier),
                    CreatedOn = DateTime.UtcNow
                };
                MapUserDtoOntoEntity(incoming, entity);
                await ReplaceUserGroupsAsync(entity, incoming.UserGroups);
                await ReplaceUserRolesAsync(entity, incoming.UserRoles);
                await _context.Set<UserModel>().AddAsync(entity);
            }
            else
            {
                entity = await _context.Set<UserModel>()
                    .Include(u => u.UserGroups)
                    .Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.Name == incoming.Name);

                if (entity == null)
                {
                    // If name provided but not found, treat as create with provided name
                    entity = new UserModel
                    {
                        Name = incoming.Name,
                        CreatedOn = DateTime.UtcNow
                    };
                    MapUserDtoOntoEntity(incoming, entity);
                    await ReplaceUserGroupsAsync(entity, incoming.UserGroups);
                    await ReplaceUserRolesAsync(entity, incoming.UserRoles);
                    await _context.Set<UserModel>().AddAsync(entity);
                }
                else
                {
                    MapUserDtoOntoEntity(incoming, entity);
                    await ReplaceUserGroupsAsync(entity, incoming.UserGroups);
                    await ReplaceUserRolesAsync(entity, incoming.UserRoles);
                }
            }

            await _context.SaveChangesAsync();
            return MapToUserDto(entity);
        }

        public override async Task<Google.Protobuf.WellKnownTypes.Empty> DeleteUser(DeleteUserRequest request, ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "name is required"));
            }

            var user = await _context.Set<UserModel>()
                .Include(u => u.UserGroups)
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Name == request.Name);

            if (user == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"User '{request.Name}' not found"));
            }

            _context.Set<UserModel>().Remove(user);
            await _context.SaveChangesAsync();
            return new Google.Protobuf.WellKnownTypes.Empty();
        }

        public override async Task<ListGroupsResponse> ListGroups(ListGroupsRequest request, ServerCallContext context)
        {
            var page = request.Page ?? new PageRequest { Skip = 0, Take = 50 };
            if (page.Take <= 0) page.Take = 50;
            if (page.Skip < 0) page.Skip = 0;

            var baseQuery = _context.Set<UserGroupModel>().AsNoTracking();
            var total = await baseQuery.CountAsync();
            var groups = await baseQuery
                .OrderBy(g => g.CreatedOn)
                .Skip(page.Skip)
                .Take(page.Take)
                .ToListAsync();

            var response = new ListGroupsResponse
            {
                Page = new PageResponse { Total = total }
            };

            foreach (var g in groups)
            {
                response.Groups.Add(new UserGroupDto
                {
                    Name = g.Name,
                    UserGroupName = g.UserGroupName ?? string.Empty,
                    Description = g.Description ?? string.Empty,
                    Status = ParseStatusStringToBool(g.Status),
                    UpdatedBy = g.UpdatedBy ?? string.Empty
                });
            }

            return response;
        }

        public override async Task<UserGroupDto> GetGroup(GetGroupRequest request, ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "name is required"));
            }

            var group = await _context.Set<UserGroupModel>().FirstOrDefaultAsync(g => g.Name == request.Name);
            if (group == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"Group '{request.Name}' not found"));
            }

            return new UserGroupDto
            {
                Name = group.Name,
                UserGroupName = group.UserGroupName ?? string.Empty,
                Description = group.Description ?? string.Empty,
                Status = ParseStatusStringToBool(group.Status),
                UpdatedBy = group.UpdatedBy ?? string.Empty
            };
        }

        public override async Task<UserGroupDto> SaveGroup(SaveGroupRequest request, ServerCallContext context)
        {
            if (request.Group == null)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "group is required"));
            }

            var incoming = request.Group;
            bool isCreate = string.IsNullOrWhiteSpace(incoming.Name);

            UserGroupModel entity;
            if (isCreate)
            {
                entity = new UserGroupModel
                {
                    Name = GenerateName(new UserGroupModel().DefaultResourceIdentifier),
                    CreatedOn = DateTime.UtcNow
                };
                entity.UserGroupName = incoming.UserGroupName ?? entity.UserGroupName;
                entity.Description = incoming.Description ?? entity.Description;
                entity.Status = StatusBoolToString(incoming.Status);
                entity.UpdatedBy = incoming.UpdatedBy ?? entity.UpdatedBy;
                entity.UpdatedOn = DateTime.UtcNow;
                await _context.Set<UserGroupModel>().AddAsync(entity);
            }
            else
            {
                entity = await _context.Set<UserGroupModel>().FirstOrDefaultAsync(g => g.Name == incoming.Name);
                if (entity == null)
                {
                    entity = new UserGroupModel
                    {
                        Name = incoming.Name,
                        CreatedOn = DateTime.UtcNow
                    };
                    await _context.Set<UserGroupModel>().AddAsync(entity);
                }

                entity.UserGroupName = incoming.UserGroupName ?? entity.UserGroupName;
                entity.Description = incoming.Description ?? entity.Description;
                entity.Status = StatusBoolToString(incoming.Status);
                entity.UpdatedBy = incoming.UpdatedBy ?? entity.UpdatedBy;
                entity.UpdatedOn = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return new UserGroupDto
            {
                Name = entity.Name,
                UserGroupName = entity.UserGroupName ?? string.Empty,
                Description = entity.Description ?? string.Empty,
                Status = ParseStatusStringToBool(entity.Status),
                UpdatedBy = entity.UpdatedBy ?? string.Empty
            };
        }

        public override async Task<Google.Protobuf.WellKnownTypes.Empty> DeleteGroup(DeleteGroupRequest request, ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "name is required"));
            }

            var group = await _context.Set<UserGroupModel>().FirstOrDefaultAsync(g => g.Name == request.Name);
            if (group == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"Group '{request.Name}' not found"));
            }

            _context.Set<UserGroupModel>().Remove(group);
            await _context.SaveChangesAsync();
            return new Google.Protobuf.WellKnownTypes.Empty();
        }

        public override async Task<ListRolesResponse> ListRoles(ListRolesRequest request, ServerCallContext context)
        {
            // Try to list roles from DB; if none exist, return a default set
            var rolesFromDb = await _context.Set<UserRoleModel>().AsNoTracking().ToListAsync();
            var response = new ListRolesResponse();

            if (rolesFromDb.Count > 0)
            {
                foreach (var r in rolesFromDb)
                {
                    response.Roles.Add(new UserRoleDto
                    {
                        Name = r.Name,
                        RoleName = r.UserRoleName ?? string.Empty,
                        Description = r.Description ?? string.Empty
                    });
                }
            }
            else
            {
                response.Roles.Add(new UserRoleDto { Name = GenerateName(new UserRoleModel().DefaultResourceIdentifier), RoleName = "ADMIN", Description = "Administrator" });
                response.Roles.Add(new UserRoleDto { Name = GenerateName(new UserRoleModel().DefaultResourceIdentifier), RoleName = "MANAGER", Description = "Manager" });
                response.Roles.Add(new UserRoleDto { Name = GenerateName(new UserRoleModel().DefaultResourceIdentifier), RoleName = "VIEWER", Description = "Viewer" });
            }

            return response;
        }
    }
}

