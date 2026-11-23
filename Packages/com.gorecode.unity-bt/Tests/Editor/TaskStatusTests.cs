using System.Linq;
using Gorecode.Bt;
using NUnit.Framework;

namespace Gorecode.Bt.Tests
{
    public class TaskStatusTests
    {
        [Test]
        public void TaskStatus_HasExactlyFourValues()
        {
            Assert.That(System.Enum.GetValues(typeof(TaskStatus)).Cast<TaskStatus>(),
                Is.EquivalentTo(new[]
                {
                    TaskStatus.InProgress,
                    TaskStatus.Success,
                    TaskStatus.Failure,
                    TaskStatus.Aborted,
                }));
        }

        [Test]
        public void TaskStatus_InProgressIsDefaultValue()
        {
            Assert.That(default(TaskStatus), Is.EqualTo(TaskStatus.InProgress));
        }
    }
}
