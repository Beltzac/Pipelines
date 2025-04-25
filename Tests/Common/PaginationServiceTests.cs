using Common.Models;
using Common.Services;
using Common.Services.Interfaces;
using FluentAssertions;
using System.Collections.Concurrent;

namespace Tests.Common
{
    public class PaginationServiceTests
    {
        private class TestState : IComparesItems<string, MongoMessage, MongoMessageDiffResult>, ITracksLoading
        {
            public Dictionary<string, MongoMessage> SourceValues { get; set; } = new();
            public Dictionary<string, MongoMessage> TargetValues { get; set; } = new();
            public ConcurrentDictionary<string, MongoMessageDiffResult> DiffCache { get; set; } = new();
            public HashSet<string> AllKeys { get; set; } = new();
            public List<MongoMessageDiffResult> PageItems { get; set; } = new();
            public int CurrentPage { get; set; } = 1;
            public int PageSize { get; set; } = 10;
            public int TotalCount { get; set; }
            public bool IsLoading { get; set; }
            public int? ProgressValue { get; set; }
            public string ProgressLabel { get; set; }
        }

        [Test]
        public async Task InitializeAsync_LoadsSourceAndTargetValues()
        {
            // Arrange
            var state = new TestState();
            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" }
                }),
                () => Task.FromResult(new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" }
                }),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult()),
                (id, source, target, diff) => Task.FromResult(true)
            );
            // Act
            await service.InitializeAsync();

            // Assert
            state.SourceValues.Should().ContainSingle();
            state.TargetValues.Should().ContainSingle();
            state.TotalCount.Should().Be(1);
        }

        [Test]
        public async Task GetPageAsync_ReturnsCorrectPageItems()
        {
            // Arrange
            var state = new TestState
            {
                PageSize = 2,
                CurrentPage = 2,
                AllKeys = new HashSet<string> { "1", "2", "3", "4", "5" },
                SourceValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" },
                    ["5"] = new() { Id = "5" }
                },
                TargetValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" },
                    ["5"] = new() { Id = "5" }
                }
            };

            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(state.SourceValues),
                () => Task.FromResult(state.TargetValues),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult { Id = id }),
                (id, source, target, diff) => Task.FromResult(true)
            );

            // Act
            await service.GetPageAsync();

            // Assert
            state.PageItems.Should().HaveCount(2);
            state.PageItems[0].Id.Should().Be("3");
            state.PageItems[1].Id.Should().Be("4");
        }

        [Test]
        public async Task GetPageAsync_AppliesFiltersCorrectly()
        {
            // Arrange
            var state = new TestState
            {
                AllKeys = new HashSet<string> { "1", "2" },
                SourceValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1", Path = "test" },
                    ["2"] = new() { Id = "2", Path = "other" }
                },
                TargetValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1", Path = "test" },
                    ["2"] = new() { Id = "2", Path = "other" }
                }
            };

            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(state.SourceValues),
                () => Task.FromResult(state.TargetValues),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult { Id = id }),
                (id, source, target, diff) => Task.FromResult(source.Path == "test") // Only include items with Path = "test"
            );

            // Act
            await service.GetPageAsync();

            // Assert
            state.PageItems.Should().ContainSingle();
            state.PageItems[0].Id.Should().Be("1");
        }

        [Test]
        public async Task GetAllFilteredDiffsAsync_UpdatesProgressCorrectly()
        {
            // Arrange
            var state = new TestState
            {
                AllKeys = new HashSet<string> { "1", "2", "3" },
                SourceValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" }
                },
                TargetValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" }
                }
            };

            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(state.SourceValues),
                () => Task.FromResult(state.TargetValues),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult { Id = id }),
                (id, source, target, diff) => Task.FromResult(true)
            );

            // Act
            await service.GetPageAsync();

            // Assert
            state.IsLoading.Should().BeFalse();
            state.ProgressValue.Should().Be(100);
        }

        [Test]
        public async Task InitializeAsync_HandlesEmptyCollections()
        {
            // Arrange
            var state = new TestState();
            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(new Dictionary<string, MongoMessage>()),
                () => Task.FromResult(new Dictionary<string, MongoMessage>()),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult()),
                (id, source, target, diff) => Task.FromResult(true)
            );

            // Act
            await service.InitializeAsync();

            // Assert
            state.SourceValues.Should().BeEmpty();
            state.TargetValues.Should().BeEmpty();
            state.TotalCount.Should().Be(0);
        }

        [Test]
        public async Task GetPageAsync_HandlesLastPageWithPartialItems()
        {
            // Arrange
            var state = new TestState
            {
                PageSize = 3,
                CurrentPage = 2,
                AllKeys = new HashSet<string> { "1", "2", "3", "4" },
                SourceValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" }
                },
                TargetValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" }
                }
            };

            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(state.SourceValues),
                () => Task.FromResult(state.TargetValues),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult { Id = id }),
                (id, source, target, diff) => Task.FromResult(true)
            );

            // Act
            await service.GetPageAsync();

            // Assert
            state.PageItems.Should().ContainSingle();
            state.PageItems[0].Id.Should().Be("4");
        }

        [Test]
        public async Task GetPageAsync_UpdatesTotalCountAfterFiltering()
        {
            // Arrange
            var state = new TestState
            {
                AllKeys = new HashSet<string> { "1", "2", "3" },
                SourceValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1", Path = "test" },
                    ["2"] = new() { Id = "2", Path = "other" },
                    ["3"] = new() { Id = "3", Path = "test" }
                },
                TargetValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1", Path = "test" },
                    ["2"] = new() { Id = "2", Path = "other" },
                    ["3"] = new() { Id = "3", Path = "test" }
                }
            };

            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(state.SourceValues),
                () => Task.FromResult(state.TargetValues),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult { Id = id }),
                (id, source, target, diff) => Task.FromResult(source.Path == "test") // Only include items with Path = "test"
            );

            // Act
            await service.GetPageAsync();

            // Assert
            state.TotalCount.Should().Be(2); // Only 2 items match the filter
        }

        [Test]
        public async Task GetPageAsync_HandlesPageSizeChanges()
        {
            // Arrange
            var state = new TestState
            {
                PageSize = 2,
                CurrentPage = 1,
                AllKeys = new HashSet<string> { "1", "2", "3", "4" },
                SourceValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" }
                },
                TargetValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" }
                }
            };

            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(state.SourceValues),
                () => Task.FromResult(state.TargetValues),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult { Id = id }),
                (id, source, target, diff) => Task.FromResult(true)
            );

            // Act - First page with size 2
            await service.GetPageAsync();
            var firstPageItems = state.PageItems.ToList();

            // Change page size
            state.PageSize = 3;
            state.CurrentPage = 1;
            await service.GetPageAsync();
            var secondPageItems = state.PageItems.ToList();

            // Assert
            firstPageItems.Should().HaveCount(2);
            secondPageItems.Should().HaveCount(3);
        }

        [Test]
        public async Task GetPageAsync_ReturnsEmptyPageWhenNoItemsMatchFilter()
        {
            // Arrange
            var state = new TestState
            {
                AllKeys = new HashSet<string> { "1", "2" },
                SourceValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1", Path = "test" },
                    ["2"] = new() { Id = "2", Path = "other" }
                },
                TargetValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1", Path = "test" },
                    ["2"] = new() { Id = "2", Path = "other" }
                }
            };

            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(state.SourceValues),
                () => Task.FromResult(state.TargetValues),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult { Id = id }),
                (id, source, target, diff) => Task.FromResult(false) // Filter that excludes all items
            );

            // Act
            await service.GetPageAsync();

            // Assert
            state.PageItems.Should().BeEmpty();
            state.TotalCount.Should().Be(0);
        }


        [Test]
        public async Task GetPageAsync_ResetsToFirstPage_WhenAllKeysChange()
        {
            // Arrange
            var state = new TestState
            {
                CurrentPage = 1,
                PageSize = 2,
                AllKeys = new HashSet<string> { "1", "2", "3", "4" },
                SourceValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" }
                },
                TargetValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" }
                }
            };

            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(state.SourceValues),
                () => Task.FromResult(state.TargetValues),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult { Id = id }),
                (id, source, target, diff) => Task.FromResult(true)
            );

            // First page load
            await service.GetPageAsync();
            state.CurrentPage.Should().Be(1);

            // Move to page 2
            state.CurrentPage = 2;
            await service.GetPageAsync();
            var initialPage = state.CurrentPage; // Capture page state *after* moving to page 2
            initialPage.Should().Be(2); // Verify we are indeed on page 2 before the change

            // Change AllKeys
            state.AllKeys = new HashSet<string> { "1", "2", "3", "4", "5" };
            state.SourceValues["5"] = new() { Id = "5" };
            state.TargetValues["5"] = new() { Id = "5" };

            // Verify reset to page 1 after getting the page again
            await service.GetPageAsync();
            state.CurrentPage.Should().Be(1); // Page should now be 1 due to reset
        }

        [Test]
        public async Task GetPageAsync_ResetsToFirstPage_WhenPageSizeChanges()
        {
            // Arrange
            var state = new TestState
            {
                CurrentPage = 1,
                PageSize = 2,
                AllKeys = new HashSet<string> { "1", "2", "3", "4" },
                SourceValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" }
                },
                TargetValues = new Dictionary<string, MongoMessage>
                {
                    ["1"] = new() { Id = "1" },
                    ["2"] = new() { Id = "2" },
                    ["3"] = new() { Id = "3" },
                    ["4"] = new() { Id = "4" }
                }
            };

            var service = new PaginationService<string, MongoMessage, MongoMessageDiffResult>(
                state,
                () => Task.FromResult(state.SourceValues),
                () => Task.FromResult(state.TargetValues),
                (id, source, target) => Task.FromResult(new MongoMessageDiffResult { Id = id }),
                (id, source, target, diff) => Task.FromResult(true)
            );

            // First page load
            await service.GetPageAsync();
            state.CurrentPage.Should().Be(1);

            // Move to page 2
            state.CurrentPage = 2;
            await service.GetPageAsync();
            var initialPage = state.CurrentPage; // Capture page state *after* moving to page 2
            initialPage.Should().Be(2); // Verify we are indeed on page 2 before the change

            // Change PageSize
            state.PageSize = 3;

            // Verify reset to page 1 after getting the page again
            await service.GetPageAsync();
            state.CurrentPage.Should().Be(1); // Page should now be 1 due to reset
        }
    }
}