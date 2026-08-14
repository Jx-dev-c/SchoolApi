namespace SchoolApi.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string? Nome { get; set; }
        public string? Email { get; set; }
        public string? Cargo { get; set; }
        public DateTime DataCriacao { get; set; }
    }
}
