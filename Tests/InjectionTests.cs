using PathOfKungfuPlugin;

namespace PathOfKungfuPlugin
{
    // The plugin sources under test are compiled into this project (see Tests.csproj); this stands in for the BepInEx plugin class.
    internal static class Plugin
    {
        internal sealed class Log
        {
            public void LogError(object message) { }
            public void LogWarning(object message) { }
            public void LogInfo(object message) { }
        }

        internal static Log Logger { get; } = new();
    }
}

namespace Tests
{
    /// <summary>The plugin's injection logic (CSV reading, row pairing, JSON cells), run on small made-up sheets.</summary>
    public class InjectionTests
    {
        public class Row
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? remark { get; set; }
            public string? talkLib { get; set; }
        }

        private sealed class Source(Dictionary<string, string> files, Dictionary<string, int[]> columns) : ITranslationSource
        {
            public TranslationTable? Get(string fileName) =>
                files.TryGetValue(fileName, out var text) ? new TranslationTable(TranslationTable.Parse(text)) : null;

            public int[]? TranslatedColumns(string fileName) => columns.GetValueOrDefault(fileName);
        }

        private static string Csv(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

        [Fact]
        public void Csv_ReadsQuotedCommasQuotesAndLineBreaks()
        {
            var records = TranslationTable.Parse("﻿1,a,\"b,c\",\"say \"\"hi\"\"\"\r\n2,x,\"two\r\nlines\",\r\n\r\n3,,,\r\n");

            Assert.Equal(3, records.Count);
            Assert.Equal(["1", "a", "b,c", "say \"hi\""], records[0]);
            Assert.Equal(["2", "x", "two\r\nlines", ""], records[1]);
            Assert.Equal(["3", "", "", ""], records[2]);
        }

        [Theory]
        [InlineData("NPC", "Dialog", "NPC__Dialog")]
        [InlineData("NPC_EN", "Dialog", "NPC__Dialog")]
        [InlineData("Touch_en", "Touch", "Touch__Touch")]
        [InlineData("item", "装备英文", "item__装备")]
        [InlineData("地图表", "MapConfigEnglish", "地图表__MapConfig")]
        [InlineData("SkillBattle", "招式配置", "SkillBattle__招式配置")]
        public void BaseName_StripsTheLanguageMarker(string workbook, string sheet, string expected) =>
            Assert.Equal(expected, SheetInjector.BaseName(workbook, sheet));

        [Fact]
        public void Apply_PairsRowsById_WhenTheGameDroppedRows_AndOnlyWritesTranslatedColumns()
        {
            // Header at row 2 (start cell A2). Packaged row 11 is a row the game did not load.
            var source = new Source(
                new() { ["Book__Sheet.csv"] = Csv("1,ID,Name,Remark", "2,id,name,remark", "3,10,Alpha,note one", "4,11,Ghost,note", "5,12,Beta,note two") },
                new() { ["Book__Sheet.csv"] = [2] });
            var list = new List<Row> { new() { id = 10, name = "甲", remark = "备注一" }, new() { id = 12, name = "乙", remark = "备注二" } };

            var stats = SheetInjector.Apply(list, "Book_en", "Sheet英文", "A2", source)!;

            Assert.Equal(2, stats.Matched);
            Assert.Equal(["Alpha", "Beta"], list.Select(r => r.name));
            Assert.Equal(["备注一", "备注二"], list.Select(r => r.remark)); // not a translated column: left as the game loaded it
        }

        [Fact]
        public void Apply_UsesTheIdColumn_NotTheFirstColumn()
        {
            // The first column is a remark that differs between the Chinese and English workbooks.
            var source = new Source(
                new() { ["B__S.csv"] = Csv("1,remark,id,name", "2,x,5,Five", "3,y,6,Six") },
                new() { ["B__S.csv"] = [3] });
            var list = new List<Row> { new() { id = 6, name = "六" }, new() { id = 5, name = "五" } };

            SheetInjector.Apply(list, "B", "S", "A1", source);

            Assert.Equal(["Six", "Five"], list.Select(r => r.name));
        }

        [Fact]
        public void Unescape_RestoresLineBreaks_ExceptWhereTheGameWritesThemLiterally()
        {
            Assert.Equal("a\nb", SheetInjector.Unescape("a\\nb", "甲\n乙"));
            Assert.Equal("a\nb", SheetInjector.Unescape("a\\nb", "甲乙"));
            Assert.Equal("a\\nb", SheetInjector.Unescape("a\\nb", "甲\\n乙"));
        }

        [Fact]
        public void Apply_TranslatesDialogueInsideJsonCells()
        {
            const string json = """[{"index":0,"talks":"[{\"id\":1,\"name\":\"甲\",\"talk\":\"你好\",\"action\":\"16:27000,25,1,守卫森严;3,2,走开\"}]"}]""";
            var source = new Source(
                new()
                {
                    ["T__T.csv"] = Csv("1,id,name,remark,talkLib", "2,7,Name,,"),
                    ["T__T.json.csv"] = Csv(
                        "1,RowId,Column,Path,Text",
                        "2,7,4,[0].talks{json}[0].name,Jia",
                        "2,7,4,[0].talks{json}[0].talk,\"Hello\\nthere\"",
                        "2,7,4,[0].talks{json}[0].action;0,\"The guard is strict; keep out\"",
                        "2,7,4,[0].talks{json}[0].action;1,Go away",
                        "2,7,4,[0].talks{json}[5].talk,missing"),
                },
                new() { ["T__T.csv"] = [] });
            var list = new List<Row> { new() { id = 7, name = "名", talkLib = json } };

            var stats = SheetInjector.Apply(list, "T", "T", "A1", source)!;

            Assert.Equal(4, stats.JsonApplied);
            Assert.Equal(1, stats.JsonMissed);
            Assert.Equal(
                """[{"index":0,"talks":"[{\"id\":1,\"name\":\"Jia\",\"talk\":\"Hello\\nthere\",\"action\":\"16:27000,25,1,The guard is strict, keep out;3,2,Go away\"}]"}]""",
                list[0].talkLib);
        }

        [Fact]
        public void Json_EnglishCellsAreAddressedByPathToo()
        {
            // In the game's English workbooks the same places hold English, so Chinese cannot be what finds them.
            const string json = """{"a":"hello","action":"5,6,go on;7,home"}""";

            var result = JsonInjector.Apply(json, new Dictionary<string, string> { [".a"] = "Hi", [".action;1"] = "Back" });

            Assert.Equal("""{"a":"Hi","action":"5,6,go on;7,Back"}""", result.Json);
        }
    }
}
