using System.Runtime.Serialization;

namespace CgStairFinder
{
    [DataContract]
    public class GithubRelease
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }
    }
}
