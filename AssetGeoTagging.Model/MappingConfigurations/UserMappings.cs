using System;
using System.Linq;
using AutoMapper;
using OlameterFramework.OFramework.BL;
using Olameter.Sdi.Assetgeotagging.V1;

// Aliases for clarity
using UserModel = AssetGeoTagging.Model.User;
using UserGroupModel = AssetGeoTagging.Model.UserGroup;
using UserRoleModel = AssetGeoTagging.Model.UserRole;
using UserGroupUserModel = AssetGeoTagging.Model.UserGroupUser;
using UserRoleUserModel = AssetGeoTagging.Model.UserRoleUser;
using UserDto = Olameter.Sdi.Assetgeotagging.V1.User;
using UserGroupDto = Olameter.Sdi.Assetgeotagging.V1.UserGroup;
using UserRoleDto = Olameter.Sdi.Assetgeotagging.V1.UserRole;

namespace AssetGeoTagging.Model.MappingConfigurations
{
    /// <summary>
    ///     AutoMapper profile containing mappings between User domain models and gRPC DTOs.
    ///     Join-entity updates (user-group, user-role) are handled in the service layer.
    /// </summary>
    public class UserMappings : AutoMapperBase
    {
        public UserMappings(IServiceProvider provider)
        {
            // Model -> DTO
            CreateMap<UserModel, UserDto>()
                // Email property is named 'Eamil' in the model
                .ForMember(d => d.Email, opt => opt.MapFrom(s => s.Eamil ?? string.Empty))
                // Map status string to boolean flag
                .ForMember(d => d.Status, opt => opt.MapFrom(s => ToBoolStatus(s.Status)))
                // Flatten join collections
                .ForMember(d => d.UserRoles, opt => opt.MapFrom(s => s.userRoleUsers.Select(j => j.UserRole)))
                .ForMember(d => d.UserGroups, opt => opt.MapFrom(s => s.userGroupUsers.Select(j => j.UserGroup)));

            CreateMap<UserGroupModel, UserGroupDto>()
                .ForMember(d => d.Status, opt => opt.MapFrom(s => ToBoolStatus(s.Status)));

            CreateMap<UserRoleModel, UserRoleDto>();

            // DTO -> Model (does not materialize join-entities; handled in service)
            CreateMap<UserDto, UserModel>()
                .ForMember(s => s.Eamil, opt => opt.MapFrom(d => d.Email))
                .ForMember(s => s.Status, opt => opt.MapFrom(d => d.Status ? "Active" : "Inactive"))
                .ForMember(s => s.userGroupUsers, opt => opt.Ignore())
                .ForMember(s => s.userRoleUsers, opt => opt.Ignore());

            CreateMap<UserGroupDto, UserGroupModel>()
                .ForMember(s => s.Status, opt => opt.MapFrom(d => d.Status ? "Active" : "Inactive"));

            CreateMap<UserRoleDto, UserRoleModel>();
        }

        private static bool ToBoolStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var normalized = status.Trim();
            return normalized.Equals("active", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("1");
        }
    }
}

