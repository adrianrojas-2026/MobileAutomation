using NUnit.Framework;

// Defines how many worker threads can execute mobile tests concurrently
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: LevelOfParallelism(3)] 
