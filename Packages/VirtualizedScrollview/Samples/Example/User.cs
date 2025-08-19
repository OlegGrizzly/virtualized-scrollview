namespace Samples.Example
{
    public class User
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int Age;

        public User(int id, string name, int age)
        {
            Id = id;
            Name = name;
            Age = age;
        }
    }
}