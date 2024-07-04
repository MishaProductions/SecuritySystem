namespace MHSApi.API
{
    public class ApiUser
    {

        /// <summary>
        /// Username of user
        /// </summary>
        public string Username { get; set; } = "";
        /// <summary>
        /// User permissions
        /// </summary>
        public UserPermissions Permissions { get; set; } = UserPermissions.None;
        /// <summary>
        /// User ID
        /// </summary>
        public int ID { get; set; } = 0;

        /// <summary>
        /// User password hash. Write only.
        /// </summary>
        public string Password { get; set; } = "";
    }

    public enum UserPermissions
    {
        /// <summary>
        /// User cannot login or do anything
        /// </summary>
        None,
        /// <summary>
        /// User can view zones and arm/disarm system.
        /// </summary>
        User,
        /// <summary>
        /// Administrator can manage settings and users.
        /// </summary>
        Admin
    }
}
