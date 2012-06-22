namespace Lithium.Tests.Models
{
	class Member
	{
		public int ID { get; set; }
		public string Name { get; set; }
		private string Username { get; set; }
	}

	struct MemberStruct
	{
		public int ID;
		public string Name { get; set; }
	}

	class Person
	{
		public int ID;
		public string Name { get; set; }
		public ContactInfo ContactInfo1 { get; set; }
		public ContactInfo ContactInfo2 { get; set; }
	}

	class ContactInfo
	{
		public City City { get; set; }
		public string Email { get; set; }
	}

	class City
	{
		public string Name { get; set; }
	}

	class CircularPerson
	{
		public string Name { get; set; }
		public CircularPerson Friend { get; set; }
	}

	public enum SomeEnum
	{
		Unknown = 0,
		One = 1,
		Two = 2,
		Three = 3
	}

	public class EnumTest
	{
		public SomeEnum SomeEnum { get; set; }
		public SomeEnum? SomeEnumNullable { get; set; }
	}
}