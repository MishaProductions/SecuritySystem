using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.API
{
    public enum UserUpdateRequestType
    {
        None,
        Password,
        Permission,
    }
    public class UserUpdateRequest
    {
        public UserUpdateRequestType type = UserUpdateRequestType.None;
    }


    internal class UserUpdatePermissionRequest
    {
        public UserUpdateRequestType type = UserUpdateRequestType.Permission;
        public UserPermissions Permissions = UserPermissions.User;

        public UserUpdatePermissionRequest(UserPermissions permissions)
        {
            Permissions = permissions;
        }
    }

    internal class UserUpdatePasswordRequest
    {
        public UserUpdateRequestType type = UserUpdateRequestType.Password;
        public string OldPassword = "";
        public string NewPassword = "";

        public string ID = "";

        public UserUpdatePasswordRequest(string oldPassword, string newPassword, string id = "")
        {
            OldPassword = oldPassword;
            NewPassword = newPassword;

            ID = id;
        }
        public UserUpdatePasswordRequest() { }
    }
}
