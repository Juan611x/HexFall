using System;

namespace HexFall.UI
{
    public enum NameAvailability
    {
        Available,
        Taken,
        Invalid,
        NetworkError,
    }

    // Asks whether a wizard name is free for a guest. Callback-shaped like IAuthService so an
    // asynchronous HTTP adapter and the in-memory fake are interchangeable.
    public interface INameAvailability
    {
        void Check(string name, Action<NameAvailability> done);
    }
}
