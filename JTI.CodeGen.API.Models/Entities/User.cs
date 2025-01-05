namespace JTI.CodeGen.API.Models.Entities
{
    public class User
    {
        public string id { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string HashedPassword { get; set; }
        public string Brand { get; set; }
        public string AppRole { get; set; }
    }
}
