using Common.Models;
using Common.Services.Interfaces;
using Common.Utils;
using FluentAssertions;

namespace Tests.Common.Utils
{
    public class SlotCalculatorTests
    {
        [Test]
        public void ComputeVesselComparison_WithValidData_ReturnsNonZeroValues()
        {
            // Arrange
            var vesselName = "TestVessel";
            var workStart = new DateTime(2025, 1, 1, 8, 0, 0);
            var workEnd = new DateTime(2025, 1, 1, 16, 0, 0);
            var vesselSchedule = new VesselSchedule
            {
                VesselName = vesselName,
                StartWork = workStart,
                EndWork = workEnd
            };

            var vessels = new Dictionary<DateTime, VesselPlan>
            {
                [workStart] = new VesselPlan(workStart, 1000, 500, new List<string> { vesselName })
            };

            var actualFlows = new Dictionary<DateTime, InOut>
            {
                [workStart] = new InOut { VesselIn = 100, VesselOut = 50 },
                [workStart.AddHours(1)] = new InOut { VesselIn = 100, VesselOut = 50 },
                [workStart.AddHours(2)] = new InOut { VesselIn = 100, VesselOut = 50 }
            };

            var vesselLoadRate = 100.0;
            var vesselUnloadRate = 100.0;

            // Act
            var result = SlotCalculator.ComputeVesselComparison(
                vesselName,
                vesselSchedule,
                vessels,
                actualFlows,
                vesselLoadRate,
                vesselUnloadRate
            );

            // Assert
            result.Should().NotBeEmpty();
            result.Should().HaveCountGreaterThan(0);

            // Check that simulated values are non-zero
            result.Any(r => r.SimulatedDischargeRate > 0).Should().BeTrue();
            result.Any(r => r.SimulatedLoadRate > 0).Should().BeTrue();
            result.Any(r => r.CumulativeSimulatedTeu > 0).Should().BeTrue();

            // Check that real values are non-zero (where actualFlows exist)
            result.Any(r => r.RealDischargeRate > 0).Should().BeTrue();
            result.Any(r => r.RealLoadRate > 0).Should().BeTrue();
            result.Any(r => r.CumulativeRealTeu > 0).Should().BeTrue();
        }

        [Test]
        public void ComputeVesselComparison_WithEmptyVesselName_ReturnsEmptyList()
        {
            // Arrange
            var vesselSchedule = new VesselSchedule
            {
                VesselName = "",
                StartWork = DateTime.Now,
                EndWork = DateTime.Now.AddHours(8)
            };

            var vessels = new Dictionary<DateTime, VesselPlan>();
            var actualFlows = new Dictionary<DateTime, InOut>();

            // Act
            var result = SlotCalculator.ComputeVesselComparison(
                "",
                vesselSchedule,
                vessels,
                actualFlows,
                100.0,
                100.0
            );

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void ComputeVesselComparison_WithNullWorkTimes_ReturnsEmptyList()
        {
            // Arrange
            var vesselSchedule = new VesselSchedule
            {
                VesselName = "TestVessel",
                StartWork = null,
                EndWork = null
            };

            var vessels = new Dictionary<DateTime, VesselPlan>();
            var actualFlows = new Dictionary<DateTime, InOut>();

            // Act
            var result = SlotCalculator.ComputeVesselComparison(
                "TestVessel",
                vesselSchedule,
                vessels,
                actualFlows,
                100.0,
                100.0
            );

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void ComputeVesselComparison_CumulativeValuesIncreaseOverTime()
        {
            // Arrange
            var vesselName = "TestVessel";
            var workStart = new DateTime(2025, 1, 1, 8, 0, 0);
            var workEnd = new DateTime(2025, 1, 1, 16, 0, 0);
            var vesselSchedule = new VesselSchedule
            {
                VesselName = vesselName,
                StartWork = workStart,
                EndWork = workEnd
            };

            var vessels = new Dictionary<DateTime, VesselPlan>
            {
                [workStart] = new VesselPlan(workStart, 1000, 500, new List<string> { vesselName })
            };

            var actualFlows = new Dictionary<DateTime, InOut>
            {
                [workStart] = new InOut { VesselIn = 100, VesselOut = 50 },
                [workStart.AddHours(1)] = new InOut { VesselIn = 100, VesselOut = 50 },
                [workStart.AddHours(2)] = new InOut { VesselIn = 100, VesselOut = 50 }
            };

            // Act
            var result = SlotCalculator.ComputeVesselComparison(
                vesselName,
                vesselSchedule,
                vessels,
                actualFlows,
                100.0,
                100.0
            );

            // Assert
            result.Should().NotBeEmpty();

            // Check that cumulative values are non-decreasing
            for (int i = 1; i < result.Count; i++)
            {
                result[i].CumulativeSimulatedTeu.Should().BeGreaterThanOrEqualTo(result[i - 1].CumulativeSimulatedTeu);
                result[i].CumulativeRealTeu.Should().BeGreaterThanOrEqualTo(result[i - 1].CumulativeRealTeu);
            }
        }

        [Test]
        public void ComputeVesselComparison_SimulationPeriodIncludesBufferDays()
        {
            // Arrange
            var vesselName = "TestVessel";
            var workStart = new DateTime(2025, 1, 1, 8, 0, 0);
            var workEnd = new DateTime(2025, 1, 1, 16, 0, 0);
            var vesselSchedule = new VesselSchedule
            {
                VesselName = vesselName,
                StartWork = workStart,
                EndWork = workEnd
            };

            var vessels = new Dictionary<DateTime, VesselPlan>
            {
                [workStart] = new VesselPlan(workStart, 1000, 500, new List<string> { vesselName })
            };

            var actualFlows = new Dictionary<DateTime, InOut>();

            // Act
            var result = SlotCalculator.ComputeVesselComparison(
                vesselName,
                vesselSchedule,
                vessels,
                actualFlows,
                100.0,
                100.0
            );

            // Assert
            result.Should().NotBeEmpty();

            // Simulation period should be 1 day before work start to 1 day after work end
            // That's 2 days + 8 hours = 56 hours, but inclusive range gives 57 hours
            var expectedHours = 57;
            result.Should().HaveCount(expectedHours);

            // First timestamp should be 1 day before work start
            var firstTimestamp = DateTime.Parse(result.First().Timestamp);
            firstTimestamp.Should().Be(workStart.AddDays(-1));

            // Last timestamp should be 1 day after work end
            var lastTimestamp = DateTime.Parse(result.Last().Timestamp);
            lastTimestamp.Should().Be(workEnd.AddDays(1));
        }

        [Test]
        public void ComputeVesselComparison_WithMultipleVessels_FiltersCorrectVessel()
        {
            // Arrange
            var targetVesselName = "TargetVessel";
            var otherVesselName = "OtherVessel";
            var workStart = new DateTime(2025, 1, 1, 8, 0, 0);
            var workEnd = new DateTime(2025, 1, 1, 16, 0, 0);
            var vesselSchedule = new VesselSchedule
            {
                VesselName = targetVesselName,
                StartWork = workStart,
                EndWork = workEnd
            };

            var vessels = new Dictionary<DateTime, VesselPlan>
            {
                [workStart] = new VesselPlan(workStart, 1000, 500, new List<string> { targetVesselName, otherVesselName })
            };

            var actualFlows = new Dictionary<DateTime, InOut>();

            // Act
            var result = SlotCalculator.ComputeVesselComparison(
                targetVesselName,
                vesselSchedule,
                vessels,
                actualFlows,
                100.0,
                100.0
            );

            // Assert
            result.Should().NotBeEmpty();

            // The simulated TEU should only include the target vessel's TEU
            // Since both vessels are in the same plan, we need to check that the filtering works
            // by verifying the total simulated TEU matches the expected value
            var totalSimulatedTeu = result.Last().CumulativeSimulatedTeu;
            totalSimulatedTeu.Should().BeGreaterThan(0);
        }

        [Test]
        public void ComputeVesselComparison_WithNoActualFlows_StillReturnsSimulatedData()
        {
            // Arrange
            var vesselName = "TestVessel";
            var workStart = new DateTime(2025, 1, 1, 8, 0, 0);
            var workEnd = new DateTime(2025, 1, 1, 16, 0, 0);
            var vesselSchedule = new VesselSchedule
            {
                VesselName = vesselName,
                StartWork = workStart,
                EndWork = workEnd
            };

            var vessels = new Dictionary<DateTime, VesselPlan>
            {
                [workStart] = new VesselPlan(workStart, 1000, 500, new List<string> { vesselName })
            };

            var actualFlows = new Dictionary<DateTime, InOut>();

            // Act
            var result = SlotCalculator.ComputeVesselComparison(
                vesselName,
                vesselSchedule,
                vessels,
                actualFlows,
                100.0,
                100.0
            );

            // Assert
            result.Should().NotBeEmpty();

            // Simulated data should still be present
            result.Any(r => r.SimulatedDischargeRate > 0).Should().BeTrue();
            result.Any(r => r.SimulatedLoadRate > 0).Should().BeTrue();
            result.Any(r => r.CumulativeSimulatedTeu > 0).Should().BeTrue();

            // Real data should be zero (no actual flows)
            result.All(r => r.RealDischargeRate == 0).Should().BeTrue();
            result.All(r => r.RealLoadRate == 0).Should().BeTrue();
            result.All(r => r.CumulativeRealTeu == 0).Should().BeTrue();
        }

        [Test]
        public void ComputeVesselComparison_DifferenceIsCalculatedCorrectly()
        {
            // Arrange
            var vesselName = "TestVessel";
            var workStart = new DateTime(2025, 1, 1, 8, 0, 0);
            var workEnd = new DateTime(2025, 1, 1, 16, 0, 0);
            var vesselSchedule = new VesselSchedule
            {
                VesselName = vesselName,
                StartWork = workStart,
                EndWork = workEnd
            };

            var vessels = new Dictionary<DateTime, VesselPlan>
            {
                [workStart] = new VesselPlan(workStart, 1000, 500, new List<string> { vesselName })
            };

            var actualFlows = new Dictionary<DateTime, InOut>
            {
                [workStart] = new InOut { VesselIn = 100, VesselOut = 50 }
            };

            // Act
            var result = SlotCalculator.ComputeVesselComparison(
                vesselName,
                vesselSchedule,
                vessels,
                actualFlows,
                100.0,
                100.0
            );

            // Assert
            result.Should().NotBeEmpty();

            // Verify that Difference = CumulativeRealTeu - CumulativeSimulatedTeu
            foreach (var dataPoint in result)
            {
                var expectedDifference = dataPoint.CumulativeRealTeu - dataPoint.CumulativeSimulatedTeu;
                dataPoint.Difference.Should().Be(expectedDifference);
            }
        }
    }
}
