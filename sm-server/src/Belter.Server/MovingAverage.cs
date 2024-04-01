namespace Belter.Server;

public class MovingAverage  
    {
        private Queue<Decimal> samples = new Queue<Decimal>();
        private readonly int windowSize;
        private Decimal sampleAccumulator;
        public Decimal Average { get; private set; }

        public MovingAverage(int windowSize)
        {
            this.windowSize=windowSize;
        }

        /// <summary>
        /// Computes a new windowed average each time a new sample arrives
        /// </summary>
        /// <param name="newSample"></param>
        public void ComputeAverage(Decimal newSample)
        {
            sampleAccumulator += newSample;
            samples.Enqueue(newSample);

            if (samples.Count > windowSize)
            {
                sampleAccumulator -= samples.Dequeue();
            }

            Average = sampleAccumulator / samples.Count;
        }
    }