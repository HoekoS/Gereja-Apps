function luminance(hex: string) {
  const channels = [1, 3, 5].map((start) => {
    const channel = parseInt(hex.slice(start, start + 2), 16) / 255;
    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
}
export function contrastRatio(first: string, second: string) {
  const a = luminance(first),
    b = luminance(second);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}
export function readableColor(preferred: string, surfaces: string[], minimum = 4.5) {
  const fits = (color: string) =>
    surfaces.every((surface) => contrastRatio(color, surface) >= minimum);
  if (fits(preferred)) return preferred;
  const endpoint = ["#101820", "#ffffff"].sort(
    (a, b) =>
      Math.min(...surfaces.map((surface) => contrastRatio(b, surface))) -
      Math.min(...surfaces.map((surface) => contrastRatio(a, surface))),
  )[0];
  for (let step = 1; step <= 100; step++) {
    const amount = step / 100;
    const candidate =
      "#" +
      [1, 3, 5]
        .map((start) =>
          Math.round(
            parseInt(preferred.slice(start, start + 2), 16) * (1 - amount) +
              parseInt(endpoint.slice(start, start + 2), 16) * amount,
          )
            .toString(16)
            .padStart(2, "0"),
        )
        .join("");
    if (fits(candidate)) return candidate;
  }
  return endpoint;
}
