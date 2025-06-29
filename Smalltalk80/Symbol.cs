public class Symbol
{
    public string Name { get; }

    public Symbol(string name)
    {
        Name = name;
    }

    public override bool Equals(object obj)
    {
        return obj is Symbol other && Name == other.Name;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    public override string ToString()
    {
        return "#" + Name;
    }
}