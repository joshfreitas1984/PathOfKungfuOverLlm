# Glossary and gender seed (plan step 3 groundwork)

Status: first seed, generated from the game's own data. Nothing here was translated by an LLM.
The generator was a throwaway script; the outputs below are the deliverable.

## What was produced

| File | Content | Active? |
| --- | --- | --- |
| [`Files/CharacterGenders.yaml`](../../Files/CharacterGenders.yaml) | 1,324 names with a gender: 1,038 male, 286 female | Yes: read by [`CharacterLineContext`](../../Translate/CharacterLineContext.cs), registered in `GameFileHandling.Hooks`; `lineContextEnabled: true` in `Config.yaml`; tests in `Tests/CharacterLineContextTests.cs` |
| [`Files/Glossary/`](../../Files/Glossary/) (22 files, about 3,030 entries) | Split by theme, see below | Yes: LlmKit loads every file in `Glossary/`; `Tests/GlossaryTests.cs` checks it |
| [`Files/Raw/GlossaryCandidates/OfficialEnglishCandidates.csv`](../../Files/Raw/GlossaryCandidates/OfficialEnglishCandidates.csv) | 10,690 Chinese strings with the English the game ships for them, counts and variants | No (reference only, not under `Glossary/`) |

## Genders

The game stores them: `NPC.xlsx` column `性别` (`sex`: **1 = male, 2 = female**; 2,247 male and 507
female rows over 1,308 distinct names) and `monster.xlsx` sheet `角色模组` column `sex` (182 male, 24
female). The coding was checked against names (`小钰` a girl, `毛毛`/`杜颜白` male). A name is listed
only if **every** row that carries it agrees. The 41 names that appear with both genders are generic
roles (`行人`, `掌柜`, `太和弟子`, `南宫侍女`, `小乞儿`...) and are left out on purpose: no entry means
unknown, never a guess. There is no entry for the player, who picks a gender when creating a
character.

Gaps and follow-ups:
- Variants such as `战损 何傲霜`, `桃卫8（女）`, `何傲霜（神秘人`, `五圣弟子（尸首）` are separate names in the
  data. Add the base name as an alias (the skill's step 2.5) when wiring the provider.
- Many entries are generic roles with a consistent gender (`人贩匪徒`, `酒铺小二`). They are harmless
  but the table is much larger than a named-character list; trim if it slows matching.
- `姓名库` (name pools) is a list of given names by gender, usable later to gender randomly named
  NPCs; not used here.
- Procedurally named NPCs are not in `NPC.xlsx`, so they have no entry.

## Glossary files

The glossary is one file per theme (the loader reads every file directly under `Glossary/`, and file names are only organisation):
`Characters`, `Sects`, `Places`, `Strongholds`, `Lairs-and-Caves`, `Gangs-and-Dungeons`, `Inns-and-Taverns`,
`Temples-and-Palaces`, `Mansions-and-Halls`, `Terrain` (names and places, about 550 entries), and the terminology files
`Stats` (resources, attack/defense, crit, attributes), `Progression` (cultivation, realms, the ten mastery tiers, the 28 talents,
life skills), `MartialArts` (weapon types, book categories, Three Talents Tome), `Combat` (actions and status effects),
`Equipment-and-Items`, `Social-and-Gangs`, `Meridians`, `Acupoints` (deterministic pinyin, 102 names; 68 with a polyphonic
character were left out), `Interface`. An entry must exist in only one file. The terminology files are hand-authored and
complement the preset (气血 = Lifeforce, 真气 = True Qi, 内功 = Neigong...); choices to confirm: 精力 = Energy (not Stamina, which is 体力),
内劲 = Inner Force, 筋骨 = Physique, 武力 = Might, the ten mastery-tier names, and the talent names (idiomatic four-character
phrases, rendered by meaning). Skill, move and item names (about 900 moves, 113 martial arts, 1,000 items) are not in the glossary yet:
they need a names-first translation pass followed by review.
**Flag strictness.** `badtrans` defaults to true, so a glossary miss flags the split. Entries that are prompt guidance only carry
`badtrans: false`; `allowalt` lists harmless variants (`Japan`/`Japanese` for 东瀛, collapsed pinyin such as `Liu Yinyin`). Preset lines
that over-flagged here (kinship terms, 将军, 庄主, 剑, 掌柜...) are overridden in `Preset-Overrides.yaml`, which repeats the preset line
with `badtrans: false` or extra `allowalt`, so the shared preset is untouched. Still enforced on purpose: 少俠, 大侠, 江湖, bracketed
move names, and real name mistakes (`第五青衫` written as `Qingshan Diwu`). `Character-Names.yaml` is hand-curated, not generated, so edits to it persist.


## Martial art, move and buff names (reviewed)

All 1,322 distinct names (348 martial arts, manuals, formulas and equipment skills; 974 moves, passives and buffs) were
translated, then **reviewed and rewritten one by one**, because they are idioms, lines of classical poetry or allusions:
`一苇横江` "A Single Reed Across the River", `潜龙勿用` / `亢龙有悔` (I Ching 乾卦, "Hidden Dragon, Do Not Act" /
"Arrogant Dragon Has Regrets"), frontier-poem moves (`故破楼兰` "Thus Loulan Is Broken", `归青冢` "Return to the
Green Tomb"), `易水留歌` (Jing Ke), `凌波微步` "Lingbo Weibu". A first pass with `qwen2.5:14b-instruct` and a
dedicated name prompt was tried: it mistranslated some outright (`沧海观澜刀法` came out as a Sword Art, `昭霞别册` as "Dawn
Dawn Companion Volume") and leaked prompt text into one name, so its output was used only as a hint.

- **`Files/ManualTranslations.yaml`** (1,322 entries) applies each reviewed name as an **exact whole-cell translation before the
  LLM**, which also settles the 2-character names (`万化`, `无涯`) that are too short for the substring glossary. Names joined
  by `·` or `-` are split into fragments by LlmKit, so each multi-character fragment has its own entry (one-character
  fragments such as `流`, `密`, `蓄` are left to the model: too likely to collide with other cells).
- **`Glossary/MartialArt-Names.yaml` (202) and `Glossary/Move-Names.yaml` (896)** carry the same names of three or more
  characters as prompt hints, so descriptions that mention `【伏邪诛阳】` use the same English.
- Checked: no name collides with a person's name in the dialogue or NPC tables (the matches are creature, item and
  equipment names that carry the same string, which is correct).
- Conventions: 剑法 Sword Art, 刀法 Blade Art, 枪法 Spear Art, 通式 Common Form, 轻击/重击 Light/Heavy Attack with I-IV for 壹-肆,
  蛊 Gu, 罡 Gang, 劲 Force, Japanese-flavoured names keep their loanwords (Iai, Zanshin, Kesa).
- To confirm: 无心流 "Mindless School", 出云无心流 "Izumo Mindless School", 煮酒 "Brewing Wine", 朝闻道 "Hearing the Dao at Dawn"
  (translated by meaning, with the Dao pinyin), and the choice to leave `蛭丸` as "Hirumaru".
- Item names were done the same way (next section).

## Item names (reviewed)

About 1,050 distinct fragments across the item sheets (equipment, medicine, food, recipes, manuals, materials, quest items,
costumes) were translated one by one after the skill names (item names embed them: `犰狳穿滚术（上册）` is the
armadillo technique's manual). Shipped-English hints were consulted but many were wrong or flat (`墨家` came out as
"Moisture", `钜子` as "Mozi", `白面土`); equipment materials are consistent (精铁 Fine Iron, 镔铁 Wootz Iron, 玄铁 Dark Steel, 蒲草 Rush,
蜡染 Batik), as are suffixes (丸 Pill, 丹 Elixir, 散 Powder, 膏 Ointment, 露 Dew, 卷 Scroll, 戒 Ring, 坠 Pendant, 缠 Wrap).

- **`Files/ManualTranslations.yaml`** now has about 2,590 entries: the 1,322 skill names plus about 1,260 item names and their
  derived forms (X烹饪配方 "Cooking Recipe", X药方 "Formula", X·精锻造配方 "Refined Forging Recipe", X（上册/下册/全本）
  "Part One / Part Two / Complete", `壹贰叁肆` as I-IV).
- **`Glossary/Item-Names.yaml`** (989) holds the names of three or more characters as prompt hints.
- Fixed along the way: the item sheets have a single header row, so the first data row of each sheet (`粗布缠手`, `还复膏`,
  `清汤面`, `一枚铜钱`) was missed by the first scan and added afterwards; `侠白` is deliberately not a manual entry because it is also an
  acupoint name (`JingLuo`), so it is left to context.
- Verified on a fresh run of the three sample files: manual names apply exactly (`理气丸` Qi-Regulating Pill), the glossary keeps
  `精力` as "Energy" and `青田鱼汤` as "Qingtian Fish Soup" inside sentences, and map names still follow the glossary. A manual
  entry replaces a split's translation only when the line is translated, so the sample files had to be re-exported first.
- Judgement calls to confirm: `墨家` Mohist and `钜子` "Mohist Juzi"; `和刀` "Wa Blade"; `武斗` "Martial Combat"; the Japanese-flavoured
  items (`御守` "Protective Charm", `百人一首` "Hyakunin Isshu", `花见` "Hanami").

## Dialogue tokens and speaker names (reviewed)

Found by running a dialogue sample before the corpus run (`SandBox_NpcFromTouch__Touch.json.csv`, 700 lines):

- **Runtime tokens split sentences.** Dialogue holds `<fName>`/`<lName>`/`<name>`/`<faction>` (a name filled in by the game) and
  gender alternatives `<少侠||姑娘>` (shown as the part before `||` to a male player, after it to a female player; 91 distinct, about
  3,400 uses). Left alone, the splitter broke each sentence at the token and translated the pieces separately ("Big Brother is so
  skilled with your hands"). `GameFileHandling.SplitterOptions` now glues them into the sentence (`PlaceholderPatterns`), the four
  dialogue files set `IgnoreHtmlTagsInText` (a gender alternative looks like an HTML tag whose content must change) with a
  `ValidateRuntimeTokens` hook that checks every name token, colour tag and gender alternative is still there, and
  `Files/Prompts/PlayerTokenPrompt.txt` tells the model how to treat them. `Glossary/Player-Gender-Tokens.yaml` gives
  the English of all 91 alternatives (`<Young Hero||Miss>`, `<Big Brother||Big Sister>`...), reviewed by hand.
  The same hook feeds `UnknownGenderPersonTokens` so a he/she beside a name token is not treated as an invented gender.
  **Named tokens.** `Config.yaml: namedTokenReplacers` now hides name tokens and title alternatives from the model behind descriptive
  slots (`{firstName}`, `{playerTitle}`...) and restores them afterwards; a title alternative is restored to its
  `Player-Gender-Tokens.yaml` result, so the model never writes the pair (it was copying `<male||female>` from the validator message
  in 77 lines) and cannot infer a gender from it. Pronoun alternatives (`<他||她>`) stay visible because he/him/his depends on the
  sentence. `ValidateRuntimeTokens` now requires an exact count of name tokens and alternatives (an extra one is invented).
  Sample result: 110 of 113 token lines keep every token; the other 3 failed validation and stay untranslated for a retry.
- **Speaker names were inconsistent** (`小钰` as "Little Yu", "Xiao Yu" and "Xiaoyu"; `糖人张` as "Tangren Zhang"). The 1,206 distinct
  speaker and NPC names not covered elsewhere were reviewed (people as pinyin, e.g. `Xiaoyu`, `Duanmu Qing`, `Tange Sazen`; roles in
  English, e.g. `Waiter`, `Old Fisherman`, `Candy Figurine Zhang`; `（尸首）` "(Corpse)", `&101`, numbers and A/B/C letters carried
  over). They are in `ManualTranslations.yaml` (exact) and `Glossary/Character-Names.yaml` (3+ characters, as hints). After the
  change every one of the 115 speakers in the sample has one translation.
- Judgement calls to confirm: pinyin without a space inside the given name (`Aoshuang`), `ü` written as `Yu`/`Lyu`, `Madam Xu
  (née Zhang)` for `许张氏`, Japanese-flavoured names kept in their Japanese reading (`Tange Sazen`, `Yagyu Munechika`, `Izumo Keiju`).

## Glossary seed (validated)

The first cut (195 people, 66 places) was taken from the game's shipped English, which the project
owner does not want used where it is poor. It was then validated and pruned:

- **People (174 kept, 21 removed).** Every English name was compared with the pinyin of the Chinese
  (`pypinyin`, any reading accepted for polyphonic characters). An entry stays only if the English
  is exactly that pinyin. The 21 removed were wrong or not names: `朱尧` "Chu Yao", `马小荷`
  "Ma Xiaohuo", `赵明澈` "Zhao Mingchu", `穆凝紫` "Mu Ningshi", `林云逍` "Lin Yun Yi", `黎大安` "Lai
  Tai On", `唐恨` "Tang Hate", `苏娘子` "Su Maiden", `赵婶` "Auntie Zhao", the Japanese
  `柳生…` names, and clan-wife forms (`李张氏` "Li Zhang"). Pinyin is deterministic, so what remains is
  correct spelling, not a trust call on the shipped data. Whether the project wants pinyin for
  people is the project's decision; surnames are not reconciled with a style guide here.
- **Places (9 kept, 57 removed).** Only plain proper-noun pinyin stayed: Dali, Luoyang, Xiangyang,
  Chang'an, Hangzhou, Cheng'an Town, Lujue Inn, Longmen Inn, Longmen Desert. Dropped because the
  shipped English was wrong or a descriptive choice that is the project's to make: `隐月楼`
  "Moonward Peak" (楼 is a tower), `拜月台` "Hidden Moon Pavilion", `桃花坞宴客厅` "Peach Feast",
  `青萍房屋密室` "Green Hideout" (青萍 is Qingping), `朝鲜` "North Korea", `无名集` "Nameless Set",
  all the "... Hideout/Brothel/Gang/Jail" compounds, and generic words (`山洞`, `京城`).
  `杭州` was added from the same rule, not from the game.
- `CharacterGenders.yaml` keeps `englishName` only for the 174 validated people; the 21 removed
  names lost theirs.

## Sects and factions (hand-authored, 41 entries)

Written for this project, not taken from the shipped English. The seven sects in `门派表/sect.csv`
(太和派, 天涯海阁, 两禅寺, 神秀峰, 五圣教, 大旗门, 大雪山) plus 隐月楼, with member forms found
in the NPC data (`太和弟子`, `隐月楼弟子`, `五圣教徒`...), the player-facing `无门无派` ("No Sect"), the
tournament terms (`武林大会`, `武林盟主`), and the larger organisations by frequency in the data
(桃花坞, 金鲤商队/商会, 壁城水寨, 连环寨, five escort agencies...). Convention: transliterate the
name (pinyin) and translate the type word, following the preset (门派 = Sect, 寨 = Stronghold, 庄 = Manor,
长老 = Elder); so `太和派` is "Taihe Sect", `隐月楼` "Yinyue Tower", `金鲤商队` "Golden Carp
Caravan". Left out because the reading is uncertain: `太平妖道` (188 mentions), `步海阁` (a skill
rather than a place) and `天刀大会` (128 mentions, an event of unclear meaning). Judgement calls to confirm:
`五圣教` "Wusheng Sect" (versus "Cult", as the preset renders `日月神教`) and `大雪山`
"Daxue Mountain". Gang-title pools (`GangName.csv`: 帮主/副帮主/长老/头领/帮众; 总舵主/舵主/坛主/香主;
教主/使者/护法/旗主/教众) are not in the glossary yet: only 帮主, 副帮主, 长老 exist in the preset.

## Map and location entries (hand-authored, about 330 more)

Added from `地图表` (all 194 map names), the named places and buildings in `Touch`/`SandBox_Touch`, and the
quest text. Conventions: 寨 = **Stronghold** (the wording the other OverLlm glossaries use, e.g. "Second
Stronghold Master" for `二寨主`; this overrides the LlmKit preset's "Fort Master" for `寨主`), 帮派 = Gang,
酒楼 = Tavern and 青楼 = Pleasure House (both from the preset), 府 = Mansion, 祠堂 = Ancestral Hall, 武馆 = Martial Arts
Hall, 地牢 = Dungeon, 秘窟 = Secret Lair, 房屋密室 = Secret Room, 客栈 = Inn, 楼 = Tower, 阁 = Pavilion, 殿 = Hall, 寺 = Temple. Real
places use their usual English (灵隐寺 Lingyin Temple, 白马寺 White Horse Temple, 龙门石窟 Longmen Grottoes, 西湖 West Lake).
Each city has its own dungeon, gambling den, tavern, pleasure house and secret room entries (`杭州地牢` Hangzhou
Dungeon). Judgement calls to confirm: `无名集` "Wuming Market", `京城` "Capital City", `东瀛` "Dongying",
`朝鲜` "Joseon", `梁君子` "Liang Junzi" (read as a person's name), `房屋密室` "Secret Room".

## What was not promoted, and why

The generic harvest has **10,690 candidates, 847 with conflicting English**, and a plain row
alignment is noisy: some columns pair a category cell with the wrong English (`单手轻刃` -> "Skill",
`晴梅花步` -> "Normal"; `天龙披山` -> "Crimson Spider Weaves Heart"), several sheets mix names,
descriptions and file-name-like quest labels (`寻踪遇鹤·四`), and `NPC_EN`/`ECA_EN` are still
**Chinese** (the shipped English NPC names live in `Touch_en`, skill names in the `SkillBattle`
English sheets). Skills, items, sects and quests therefore need an actual translation pass, with
the candidates CSV as a hint column, not as authority.

## Suggested next steps

1. Translate the rest of the names
   in a small first LLM run (needs Config.yaml models). Name lists to start with: NPC names without
   a surname, sects (`门派表`), skills (`招式配置`), items.
2. Gender context is wired. Limitation: dialogue is flattened to one string per row and the speaker is
   a separate `name` row, so a `talk` line that does not name anyone gets no gender context. Giving
   `talk` rows their speaker is a later step.
3. Add generic-role handling: decide how "a merchant" style names should translate before they are
   dedup-translated once and reused everywhere.
