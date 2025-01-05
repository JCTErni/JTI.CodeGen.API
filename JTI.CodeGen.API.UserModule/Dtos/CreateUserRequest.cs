namespace JTI.CodeGen.API.UserModule.Dtos
{
    public class CreateUserRequest
    {
        public string Email { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Brand { get; set; }
        public string AppRole { get; set; }
    }
}
