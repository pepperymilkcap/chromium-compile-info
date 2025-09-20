using System;
using Xunit;
using ChromiumCompileMonitor.Services;

namespace ChromiumCompileMonitor.Tests
{
    public class ProgressParserTests
    {
        private readonly ProgressParser _parser;

        public ProgressParserTests()
        {
            _parser = new ProgressParser();
        }

        [Theory]
        [InlineData("[100/900] 5m30s", 100, 900, 330)] // 5m30s = 330 seconds
        [InlineData("[250/750] 12m45s", 250, 750, 765)] // 12m45s = 765 seconds
        [InlineData("[500/500] 1h5m30s", 500, 500, 3930)] // 1h5m30s = 3930 seconds
        [InlineData("[999/1] 2h15m45s", 999, 1, 8145)] // 2h15m45s = 8145 seconds
        public void ParseLine_ValidInput_ReturnsCorrectProgress(string input, int expectedCompiled, int expectedRemaining, int expectedElapsedSeconds)
        {
            // Act
            var result = _parser.ParseLine(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedCompiled, result.CompiledBlocks);
            Assert.Equal(expectedRemaining, result.RemainingBlocks);
            Assert.Equal(expectedElapsedSeconds, result.ElapsedTime.TotalSeconds);
            Assert.Equal(expectedCompiled + expectedRemaining, result.TotalBlocks);
        }

        [Theory]
        [InlineData("[100/900] 5m30s", 10.0)] // 100/1000 = 10%
        [InlineData("[250/750] 12m45s", 25.0)] // 250/1000 = 25%
        [InlineData("[500/500] 1h5m30s", 50.0)] // 500/1000 = 50%
        [InlineData("[999/1] 2h15m45s", 99.9)] // 999/1000 = 99.9%
        public void ParseLine_ValidInput_CalculatesCorrectPercentage(string input, double expectedPercentage)
        {
            // Act
            var result = _parser.ParseLine(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedPercentage, result.PercentageCompleted, 1); // 1 decimal place precision
        }

        [Theory]
        [InlineData("[100/900] 5m30s", 3.3)] // 330s / 100 blocks = 3.3s per block
        [InlineData("[250/750] 12m45s", 3.06)] // 765s / 250 blocks = 3.06s per block
        [InlineData("[500/500] 1h5m30s", 7.86)] // 3930s / 500 blocks = 7.86s per block
        public void ParseLine_ValidInput_CalculatesCorrectTimePerBlock(string input, double expectedTimePerBlock)
        {
            // Act
            var result = _parser.ParseLine(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedTimePerBlock, result.TimePerBlock, 1); // 1 decimal place precision
        }

        [Theory]
        [InlineData("[100/900] 5m")]
        [InlineData("[250/750] 45s")]
        [InlineData("[500/500] 300")]
        [InlineData("[999/1] 1h30m0s")]
        public void ParseLine_DifferentTimeFormats_ParsesSuccessfully(string input)
        {
            // Act
            var result = _parser.ParseLine(input);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.CompiledBlocks > 0);
            Assert.True(result.RemainingBlocks >= 0);
            Assert.True(result.ElapsedTime.TotalSeconds > 0);
        }

        [Theory]
        [InlineData("Invalid line")]
        [InlineData("[abc/def] 5m30s")]
        [InlineData("[100] 5m30s")]
        [InlineData("100/900 5m30s")]
        [InlineData("[100/900]")]
        [InlineData("")]
        [InlineData(null)]
        public void ParseLine_InvalidInput_ReturnsNull(string input)
        {
            // Act
            var result = _parser.ParseLine(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ParseLine_SequentialCalls_CalculatesSpeedTrend()
        {
            // Arrange
            var firstLine = "[100/900] 5m30s"; // 3.3s per block
            var secondLine = "[200/800] 10m"; // 3.0s per block (improvement)

            // Act
            var firstResult = _parser.ParseLine(firstLine);
            var secondResult = _parser.ParseLine(secondLine);

            // Assert
            Assert.NotNull(firstResult);
            Assert.NotNull(secondResult);
            Assert.Equal("Initial", firstResult.SpeedTrend);
            Assert.True(secondResult.SpeedTrend == "Sped up" || secondResult.SpeedTrend == "Steady");
        }

        [Theory]
        [InlineData("5m30s", 330)]
        [InlineData("1h5m30s", 3930)]
        [InlineData("2h15m45s", 8145)]
        [InlineData("45s", 45)]
        [InlineData("300", 300)]
        [InlineData("5m", 300)]
        public void ParseLine_VariousTimeFormats_ParsesCorrectly(string timeFormat, int expectedSeconds)
        {
            // Arrange
            var input = $"[100/900] {timeFormat}";

            // Act
            var result = _parser.ParseLine(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedSeconds, result.ElapsedTime.TotalSeconds);
        }
    }
}