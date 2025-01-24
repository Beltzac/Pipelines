using Common.Models;
using Common.Utils;
using FluentAssertions;
using System;

namespace Tests.Common.Utils
{
    public class RepositoryExtensionsTests
    {
        [Test]
        public void SecondsToNextUpdate_ShouldReturnCorrectValue_WhenLastUpdatedIsNull()
        {
            // Arrange
            var repo = new Repository { Pipeline = new Pipeline { Last = null } };

            // Act
            var result = repo.SecondsToNextUpdate();

            // Assert
            result.Should().BeGreaterThan(0);
        }

        [Test]
        public void SecondsToNextUpdate_ShouldReturnScaledValuePlusRandomOffset()
        {
            // Arrange
            var repo = new Repository
            {
                Pipeline = new Pipeline
                {
                    Last = new Build
                    {
                        Changed = DateTime.UtcNow.AddHours(-5)
                    }
                }
            };

            // Act
            var result = repo.SecondsToNextUpdate();

            // Assert
            result.Should().BeGreaterThan(0);
        }

        [Test]
        public void ScaleValue_ShouldReturnMinValue_WhenValueIsBelowMinTimeSinceUpdate()
        {
            // Arrange
            int value = 0;
            int minValue = 180;
            int maxValue = 14400;
            int minTimeSinceUpdate = 21600;
            int maxTimeSinceUpdate = 2592000;

            // Act
            var result = RepositoryExtensions.ScaleValue(value, minValue, maxValue, minTimeSinceUpdate, maxTimeSinceUpdate);

            // Assert
            result.Should().Be(minValue);
        }

        [Test]
        public void ScaleValue_ShouldReturnMaxValue_WhenValueIsAboveMaxTimeSinceUpdate()
        {
            // Arrange
            int value = 2592001;
            int minValue = 180;
            int maxValue = 14400;
            int minTimeSinceUpdate = 21600;
            int maxTimeSinceUpdate = 2592000;

            // Act
            var result = RepositoryExtensions.ScaleValue(value, minValue, maxValue, minTimeSinceUpdate, maxTimeSinceUpdate);

            // Assert
            result.Should().Be(maxValue);
        }

        [Test]
        public void ScaleValue_ShouldScaleCorrectly_WhenValueIsWithinRange()
        {
            // Arrange
            int value = 43200;
            int minValue = 180;
            int maxValue = 14400;
            int minTimeSinceUpdate = 21600;
            int maxTimeSinceUpdate = 2592000;

            // Act
            var result = RepositoryExtensions.ScaleValue(value, minValue, maxValue, minTimeSinceUpdate, maxTimeSinceUpdate);

            // Assert
            result.Should().BeGreaterThan(minValue).And.BeLessThan(maxValue);
        }
    }
}
