namespace Transat;

public class ResilientPublisher
{
    private readonly string _id = Guid.NewGuid().ToString("n");
    private readonly FaillibleQos0Storage storage1;
    private readonly FaillibleQos0Storage storage2;

    // HashSet, for no repetitions
    private readonly HashSet<int> savedToStorage1 = new();
    private readonly HashSet<int> savedToStorage2 = new();

    public ResilientPublisher(FaillibleQos0Storage storage1, FaillibleQos0Storage storage2)
    {
        this.storage1 = storage1;
        this.storage2 = storage2;
    }

    public void Send(int message)
    {
        // Storage 1
        while (!savedToStorage1.Contains(message))
        {
            storage1.Store(_id, message);

            if (WasStored(storage1, message))
                savedToStorage1.Add(message);

            Thread.Sleep(1); 
        }

        // Storage 2
        while (!savedToStorage2.Contains(message))
        {
            storage2.Store(_id, message);

            if (WasStored(storage2, message))
                savedToStorage2.Add(message);

            Thread.Sleep(1);
        }
    }

    // if message exist in storage
    private bool WasStored(FaillibleQos0Storage storage, int message)
    {
        return storage.Values.Contains(message);
    }
}
