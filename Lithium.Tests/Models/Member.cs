namespace Lithium.Tests.Models
{
	class Member
	{
		public int ID { get; set; }
		public string Name { get; set; }
		public MemberType MemberType { get; set; }
	}

	public enum MemberType
	{
		Unknown = -1,
		Normal = 1,
		Administrator = 2
	}
}
