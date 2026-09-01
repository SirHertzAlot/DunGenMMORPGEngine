namespace DunGen.Core
{
    public interface ISimulationClock
    {
        uint FrameNumber { get; }
        float Timestamp { get; }
        void Reset();
        void SetFrame(uint frameNumber);
    }

    public sealed class FixedStepSimulationClock : ISimulationClock
    {
        private readonly float _fixedStep;

        public FixedStepSimulationClock(float fixedStep = 1f / 60f)
        {
            _fixedStep = fixedStep;
        }

        public uint FrameNumber { get; private set; }

        public float Timestamp => FrameNumber * _fixedStep;

        public void Reset()
        {
            FrameNumber = 0;
        }

        public void SetFrame(uint frameNumber)
        {
            FrameNumber = frameNumber;
        }
    }
}
