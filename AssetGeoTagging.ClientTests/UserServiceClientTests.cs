using System;
using System.Linq;
using AssetGeoTagging.Client;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Olameter.Sdi.Assetgeotagging.V1;
using OlameterFramework.OFramework.ConfigUtils;
using OlameterFramework.OFramework.Utils;

namespace AssetGeoTagging.ClientTests
{
    [TestClass]
    public class UserServiceClientTests
    {
        private UserServiceClient _client;
        private string _serviceUrl;

        public UserServiceClientTests()
        {
            var (_, cong, _) = LoggerUtil.AutoInitConsole<UserServiceClientTests>(addGelfLogging: false, addZooKeeperConfig: false);
            ConfigUtil.SetAppConfig(cong);

            _serviceUrl = ConfigUtil.GetValue("serviceUrl");
        }

        [TestInitialize]
        public void Initialize()
        {
            _client = new UserServiceClient(_serviceUrl);
        }

        [TestMethod]
        public void SaveUser_Success()
        {
            var newUser = new User
            {
                UserName = $"tester_{Guid.NewGuid():N}",
                Email = "tester@example.com",
                PhoneNumber = "+10000000000",
                Title = "QA",
                Status = true,
                UpdatedBy = "test"
            };

            var saved = _client.SaveUser(newUser);

            Assert.IsFalse(string.IsNullOrWhiteSpace(saved.Name));
            Assert.AreEqual(newUser.UserName, saved.UserName);
            Assert.AreEqual(newUser.Email, saved.Email);
            Assert.IsTrue(saved.Status);
        }

        [TestMethod]
        public void GetUser_Success()
        {
            var newUser = new User
            {
                UserName = $"getter_{Guid.NewGuid():N}",
                Email = "getter@example.com",
                Status = true,
                UpdatedBy = "test"
            };

            var saved = _client.SaveUser(newUser);
            var fetched = _client.GetUser(saved.Name);

            Assert.IsNotNull(fetched);
            Assert.AreEqual(saved.Name, fetched.Name);
            Assert.AreEqual(newUser.UserName, fetched.UserName);
        }

        [TestMethod]
        public void ListUsers_Success()
        {
            var response = _client.ListUsersRaw(skip: 0, take: 5);
            Assert.IsNotNull(response.Page);
            Assert.IsTrue(response.Page.Total >= 0);
        }

        [TestMethod]
        public void DeleteUser_Success()
        {
            var newUser = new User
            {
                UserName = $"deleter_{Guid.NewGuid():N}",
                Email = "deleter@example.com",
                Status = true,
                UpdatedBy = "test"
            };

            var saved = _client.SaveUser(newUser);
            _client.DeleteUser(saved.Name);

            try
            {
                _client.GetUser(saved.Name);
                Assert.Fail("Expected NotFound after delete.");
            }
            catch (RpcException ex)
            {
                Assert.AreEqual(StatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public void SaveGroup_Success()
        {
            var group = new UserGroup
            {
                UserGroupName = $"group_{Guid.NewGuid():N}",
                Description = "Test group",
                Status = true,
                UpdatedBy = "test"
            };

            var saved = _client.SaveGroup(group);
            Assert.IsFalse(string.IsNullOrWhiteSpace(saved.Name));
            Assert.AreEqual(group.UserGroupName, saved.UserGroupName);
            Assert.IsTrue(saved.Status);
        }

        [TestMethod]
        public void GetGroup_Success()
        {
            var group = new UserGroup
            {
                UserGroupName = $"group_get_{Guid.NewGuid():N}",
                Description = "Test group get",
                Status = true,
                UpdatedBy = "test"
            };

            var saved = _client.SaveGroup(group);
            var fetched = _client.GetGroup(saved.Name);
            Assert.IsNotNull(fetched);
            Assert.AreEqual(saved.Name, fetched.Name);
        }

        [TestMethod]
        public void ListGroups_Success()
        {
            var response = _client.ListGroupsRaw(skip: 0, take: 5);
            Assert.IsNotNull(response.Page);
            Assert.IsTrue(response.Page.Total >= 0);
        }

        [TestMethod]
        public void DeleteGroup_Success()
        {
            var group = new UserGroup
            {
                UserGroupName = $"group_del_{Guid.NewGuid():N}",
                Description = "Test group del",
                Status = false,
                UpdatedBy = "test"
            };

            var saved = _client.SaveGroup(group);
            _client.DeleteGroup(saved.Name);

            try
            {
                _client.GetGroup(saved.Name);
                Assert.Fail("Expected NotFound after delete.");
            }
            catch (RpcException ex)
            {
                Assert.AreEqual(StatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public void ListRoles_Success()
        {
            var roles = _client.ListRoles();
            Assert.IsTrue(roles != null && roles.Count >= 0);
        }
    }
}

