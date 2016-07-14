using System.Threading.Tasks;

namespace NCli
{
    public interface IVerb
    {
        string OriginalVerb { get; set; }

        Task Run();
    }
}
