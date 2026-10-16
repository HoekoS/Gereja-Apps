package bible

import (
	"regexp"
	"strconv"
	"strings"
)

// Reference is a place in the Bible, independent of translation. VerseEnd is
// 0 for a whole chapter, and equal to VerseStart for a single verse — the
// operator asked for one verse, not for a range that happens to be one long.
type Reference struct {
	BookID     int
	Chapter    int
	VerseStart int
	VerseEnd   int
}

var referencePattern = regexp.MustCompile(
	`^(?P<book>.+?)\s*(?P<chapter>\d+)(?::(?P<start>\d+)(?:\s*-\s*(?P<end>\d+))?)?$`)

// ParseReference parses free-form operator input. It returns ok=false for
// anything it does not understand — an unparseable reference is an ordinary
// outcome of typing, not an exceptional condition.
func ParseReference(input string) (Reference, bool) {
	trimmed := strings.TrimSpace(input)
	if trimmed == "" {
		return Reference{}, false
	}

	match := referencePattern.FindStringSubmatch(trimmed)
	if match == nil {
		return Reference{}, false
	}
	group := groups(referencePattern, match)

	bookID, ok := Resolve(group["book"])
	if !ok {
		return Reference{}, false
	}

	chapter, err := strconv.Atoi(group["chapter"])
	if err != nil || chapter < 1 {
		return Reference{}, false
	}

	if group["start"] == "" {
		return Reference{BookID: bookID, Chapter: chapter, VerseStart: 1}, true
	}

	start, err := strconv.Atoi(group["start"])
	if err != nil {
		return Reference{}, false
	}
	end := start
	if group["end"] != "" {
		end, err = strconv.Atoi(group["end"])
		if err != nil {
			return Reference{}, false
		}
	}
	if start < 1 || end < start {
		return Reference{}, false
	}

	return Reference{BookID: bookID, Chapter: chapter, VerseStart: start, VerseEnd: end}, true
}

// groups maps a pattern's named capture groups to their matched text, for a
// match already known to have succeeded.
func groups(pattern *regexp.Regexp, match []string) map[string]string {
	result := make(map[string]string, len(match))
	for i, name := range pattern.SubexpNames() {
		if name != "" {
			result[name] = match[i]
		}
	}
	return result
}
