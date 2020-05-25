namespace RayshiftTranslateFGO.Models
{
    public class BaseAPIResponse
    {
        public int Status { get; set; }
        public string Message { get; set; }

        public object Response { get; set; }
    }
}