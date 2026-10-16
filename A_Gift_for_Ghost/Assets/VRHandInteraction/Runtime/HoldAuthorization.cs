namespace Ghost.VRHandInteraction
{
    /// <summary>Device-independent authorization; time belongs to one continuous candidate contact.</summary>
    public sealed class HoldAuthorization
    {
        public object Candidate { get; private set; }
        public bool Authorized { get; private set; }
        double startedAt;

        public void Reset() { Candidate = null; Authorized = false; startedAt = 0; }

        public bool Tick(object candidate, bool pressed, double now, float duration)
        {
            if (!pressed || candidate == null) { Reset(); return false; }
            if (!ReferenceEquals(candidate, Candidate))
            {
                Candidate = candidate;
                startedAt = now;
                Authorized = false;
            }
            Authorized = now - startedAt >= System.Math.Max(0, duration);
            return Authorized;
        }
    }
}
