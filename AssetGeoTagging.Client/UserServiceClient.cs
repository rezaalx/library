using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Olameter.Sdi.Assetgeotagging.V1;
using OlameterFramework.OFramework.gRPC;

namespace AssetGeoTagging.Client
{
    public class UserServiceClient : BaseCoreGrpcClient<UserService.UserServiceClient>
    {
        public UserServiceClient(string host) : base(host)
        {
        }

        public List<User> ListUsers(int skip = 0, int take = 50)
        {
            var request = new ListUsersRequest { Page = new PageRequest { Skip = skip, Take = take } };
            var response = Client.ListUsers(request);
            return response.Users.ToList();
        }

        public ListUsersResponse ListUsersRaw(int skip = 0, int take = 50)
        {
            var request = new ListUsersRequest { Page = new PageRequest { Skip = skip, Take = take } };
            return Client.ListUsers(request);
        }

        public User GetUser(string name)
        {
            var request = new GetUserRequest { Name = name };
            return Client.GetUser(request);
        }

        public User SaveUser(User user)
        {
            var request = new SaveUserRequest { User = user };
            return Client.SaveUser(request);
        }

        public void DeleteUser(string name)
        {
            var request = new DeleteUserRequest { Name = name };
            Client.DeleteUser(request);
        }

        public List<UserGroup> ListGroups(int skip = 0, int take = 50)
        {
            var request = new ListGroupsRequest { Page = new PageRequest { Skip = skip, Take = take } };
            var response = Client.ListGroups(request);
            return response.Groups.ToList();
        }

        public ListGroupsResponse ListGroupsRaw(int skip = 0, int take = 50)
        {
            var request = new ListGroupsRequest { Page = new PageRequest { Skip = skip, Take = take } };
            return Client.ListGroups(request);
        }

        public UserGroup GetGroup(string name)
        {
            var request = new GetGroupRequest { Name = name };
            return Client.GetGroup(request);
        }

        public UserGroup SaveGroup(UserGroup group)
        {
            var request = new SaveGroupRequest { Group = group };
            return Client.SaveGroup(request);
        }

        public void DeleteGroup(string name)
        {
            var request = new DeleteGroupRequest { Name = name };
            Client.DeleteGroup(request);
        }

        public List<UserRole> ListRoles()
        {
            var response = Client.ListRoles(new ListRolesRequest());
            return response.Roles.ToList();
        }
    }
}

