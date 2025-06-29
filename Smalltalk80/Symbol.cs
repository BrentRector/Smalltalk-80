using System;

public class Symbol {
    public string Name { get; }

    public Symbol(string name) {
        Name = name;
    }

    public override bool Equals(object? obj) => obj is Symbol other && Name == other.Name;
    public override int GetHashCode() => Name.GetHashCode();
    public override string ToString() => "#" + Name;
}