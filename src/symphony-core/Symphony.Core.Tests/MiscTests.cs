using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Symphony.Core
{
    [TestFixture]
    class ConcurrentDequeTests
    {
        [Test]
        public void ShouldEnqueueConcurrently()
        {
            var q = new ConcurrentDeque<int>();

            const int count = 1000;
            var enqueue = new Action(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        q.EnqueueFirst(i);
                    }
                });

            var t1 = new Thread(new ThreadStart(enqueue));
            var t2 = new Thread(new ThreadStart(enqueue));
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
            
            Assert.That(q.Count == count * 2);

            int last;
            q.TryPeekLast(out last);
            Assert.That(last == 0);
        }

        [Test]
        public void ShouldDequeueConcurrently()
        {
            const int count = 2000;
            var q = new ConcurrentDeque<int>(Enumerable.Range(0, count));

            var dequeue = new Action(() =>
                {
                    for (int i = 0; i < count/4; i++)
                    {
                        int ignore;
                        q.TryDequeueFirst(out ignore);
                    }
                });

            var t1 = new Thread(new ThreadStart(dequeue));
            var t2 = new Thread(new ThreadStart(dequeue));
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            Assert.That(q.Count == count / 2);
        }
    }
}
