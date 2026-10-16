// Package bible holds translation-independent rules for naming and
// addressing passages: canonical book ids and free-form reference parsing.
// Nothing here touches net/http, pgx or Redis, so it tests without a
// database — the port's first slice, ahead of any context, table or route.
package bible

import (
	"strconv"
	"strings"
)

// bookNames holds the canonical book id, the Indonesian and English names an
// operator actually types, and their abbreviations. Ids follow the usual
// Protestant order, but nothing here enforces 1..66 — a deuterocanonical
// import may carry higher ids, and Resolve simply will not know their names.
var bookNames = [][5]string{
	{"1", "Kejadian", "Kej", "Genesis", "Gen"},
	{"2", "Keluaran", "Kel", "Exodus", "Exod"},
	{"3", "Imamat", "Im", "Leviticus", "Lev"},
	{"4", "Bilangan", "Bil", "Numbers", "Num"},
	{"5", "Ulangan", "Ul", "Deuteronomy", "Deut"},
	{"6", "Yosua", "Yos", "Joshua", "Josh"},
	{"7", "Hakim-Hakim", "Hak", "Judges", "Judg"},
	{"8", "Rut", "Rut", "Ruth", "Ruth"},
	{"9", "1 Samuel", "1Sam", "1 Samuel", "1Sam"},
	{"10", "2 Samuel", "2Sam", "2 Samuel", "2Sam"},
	{"11", "1 Raja-Raja", "1Raj", "1 Kings", "1Kgs"},
	{"12", "2 Raja-Raja", "2Raj", "2 Kings", "2Kgs"},
	{"13", "1 Tawarikh", "1Taw", "1 Chronicles", "1Chr"},
	{"14", "2 Tawarikh", "2Taw", "2 Chronicles", "2Chr"},
	{"15", "Ezra", "Ezr", "Ezra", "Ezra"},
	{"16", "Nehemia", "Neh", "Nehemiah", "Neh"},
	{"17", "Ester", "Est", "Esther", "Esth"},
	{"18", "Ayub", "Ayb", "Job", "Job"},
	{"19", "Mazmur", "Mzm", "Psalms", "Ps"},
	{"20", "Amsal", "Ams", "Proverbs", "Prov"},
	{"21", "Pengkhotbah", "Pkh", "Ecclesiastes", "Eccl"},
	{"22", "Kidung Agung", "Kid", "Song of Songs", "Song"},
	{"23", "Yesaya", "Yes", "Isaiah", "Isa"},
	{"24", "Yeremia", "Yer", "Jeremiah", "Jer"},
	{"25", "Ratapan", "Rat", "Lamentations", "Lam"},
	{"26", "Yehezkiel", "Yeh", "Ezekiel", "Ezek"},
	{"27", "Daniel", "Dan", "Daniel", "Dan"},
	{"28", "Hosea", "Hos", "Hosea", "Hos"},
	{"29", "Yoel", "Yl", "Joel", "Joel"},
	{"30", "Amos", "Am", "Amos", "Amos"},
	{"31", "Obaja", "Ob", "Obadiah", "Obad"},
	{"32", "Yunus", "Yun", "Jonah", "Jonah"},
	{"33", "Mikha", "Mi", "Micah", "Mic"},
	{"34", "Nahum", "Nah", "Nahum", "Nah"},
	{"35", "Habakuk", "Hab", "Habakkuk", "Hab"},
	{"36", "Zefanya", "Zef", "Zephaniah", "Zeph"},
	{"37", "Hagai", "Hag", "Haggai", "Hag"},
	{"38", "Zakharia", "Za", "Zechariah", "Zech"},
	{"39", "Maleakhi", "Mal", "Malachi", "Mal"},
	{"40", "Matius", "Mat", "Matthew", "Matt"},
	{"41", "Markus", "Mrk", "Mark", "Mark"},
	{"42", "Lukas", "Luk", "Luke", "Luke"},
	{"43", "Yohanes", "Yoh", "John", "John"},
	{"44", "Kisah Para Rasul", "Kis", "Acts", "Acts"},
	{"45", "Roma", "Rm", "Romans", "Rom"},
	{"46", "1 Korintus", "1Kor", "1 Corinthians", "1Cor"},
	{"47", "2 Korintus", "2Kor", "2 Corinthians", "2Cor"},
	{"48", "Galatia", "Gal", "Galatians", "Gal"},
	{"49", "Efesus", "Ef", "Ephesians", "Eph"},
	{"50", "Filipi", "Flp", "Philippians", "Phil"},
	{"51", "Kolose", "Kol", "Colossians", "Col"},
	{"52", "1 Tesalonika", "1Tes", "1 Thessalonians", "1Thess"},
	{"53", "2 Tesalonika", "2Tes", "2 Thessalonians", "2Thess"},
	{"54", "1 Timotius", "1Tim", "1 Timothy", "1Tim"},
	{"55", "2 Timotius", "2Tim", "2 Timothy", "2Tim"},
	{"56", "Titus", "Tit", "Titus", "Titus"},
	{"57", "Filemon", "Flm", "Philemon", "Phlm"},
	{"58", "Ibrani", "Ibr", "Hebrews", "Heb"},
	{"59", "Yakobus", "Yak", "James", "Jas"},
	{"60", "1 Petrus", "1Ptr", "1 Peter", "1Pet"},
	{"61", "2 Petrus", "2Ptr", "2 Peter", "2Pet"},
	{"62", "1 Yohanes", "1Yoh", "1 John", "1John"},
	{"63", "2 Yohanes", "2Yoh", "2 John", "2John"},
	{"64", "3 Yohanes", "3Yoh", "3 John", "3John"},
	{"65", "Yudas", "Yud", "Jude", "Jude"},
	{"66", "Wahyu", "Why", "Revelation", "Rev"},
}

var (
	aliases   map[string]int
	canonical map[int]string
)

func init() {
	aliases = make(map[string]int, len(bookNames)*4)
	canonical = make(map[int]string, len(bookNames))
	for _, row := range bookNames {
		id, err := strconv.Atoi(row[0])
		if err != nil {
			panic("bible: non-numeric book id in table: " + row[0])
		}
		canonical[id] = row[1]
		for _, name := range row[1:] {
			aliases[normalise(name)] = id
		}
	}
}

// normalise lowercases and strips whitespace and periods, so "1 Korintus",
// "1korintus" and "1 Kor." all collapse to one key.
func normalise(name string) string {
	return strings.Map(func(c rune) rune {
		switch c {
		case ' ', '\t', '\n', '\r', '.':
			return -1
		default:
			return c
		}
	}, strings.ToLower(name))
}

// Resolve looks up any spelling in the table (Indonesian or English, full
// name or abbreviation, case-insensitive) and returns its canonical book id.
func Resolve(name string) (int, bool) {
	id, ok := aliases[normalise(name)]
	return id, ok
}

// BookName returns the Indonesian display name, or "" for an id not in the
// table.
func BookName(bookID int) (string, bool) {
	name, ok := canonical[bookID]
	return name, ok
}
