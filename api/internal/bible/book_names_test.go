package bible

import "testing"

func TestResolve(t *testing.T) {
	cases := []struct {
		name string
		want int
	}{
		{"Yohanes", john},
		{"yohanes", john},
		{"Yoh", john},
		{"John", john},
		{"1 Korintus", firstCorinthians},
		{"1korintus", firstCorinthians},
		{"1 Kor.", firstCorinthians},
		{"Kejadian", genesis},
	}

	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got, ok := Resolve(c.name)
			if !ok || got != c.want {
				t.Errorf("Resolve(%q) = (%d, %v), want (%d, true)", c.name, got, ok, c.want)
			}
		})
	}
}

func TestResolveUnknownNameReportsNotFound(t *testing.T) {
	if _, ok := Resolve("Tobit"); ok {
		t.Error(`Resolve("Tobit") reported found, want not found`)
	}
}

func TestBookName(t *testing.T) {
	name, ok := BookName(john)
	if !ok || name != "Yohanes" {
		t.Errorf("BookName(%d) = (%q, %v), want (%q, true)", john, name, ok, "Yohanes")
	}
}

func TestBookNameUnknownIDReportsNotFound(t *testing.T) {
	if _, ok := BookName(9999); ok {
		t.Error("BookName(9999) reported found, want not found")
	}
}
