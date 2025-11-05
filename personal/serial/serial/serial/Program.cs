// See https://aka.ms/new-console-template for more information


using System.Text.Json;

Character character = new Character { FirstName="Mykola", LastName="Hohol", Description="real one", PlayedBy = null };
string charachterSerialiezd = JsonSerializer.Serialize(character);
File.WriteAllText("character.json", charachterSerialiezd);

public class Character
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Description { get; set; }
    public Actor PlayedBy { get; set; }
}
public class Actor
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime BirthDate { get; set; }
    public string Country { get; set; }
    public bool IsAlive { get; set; }
}
/*
class Character
{
    private string firstName = FirstName;
    private string lastName = LastName;
    private string description = Description;
    private string playedBy = PlayedBy;

    public string FirstName { get => firstName; set => firstName = value; }
    public string LastName { get => lastName; set => lastName = value; }
    public string Description { get => description; set => description = value; }
    public string PlayedBy { get => playedBy; set => playedBy = value; }
}*/
