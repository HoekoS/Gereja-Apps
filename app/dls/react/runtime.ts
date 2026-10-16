const classicScripts = new Map<string, Promise<void>>();
export function loadClassicScript(src: string, documentOwner = document): Promise<void> {
  const url = new URL(src, documentOwner.baseURI).href;
  let pending = classicScripts.get(url);
  if (!pending) {
    pending = new Promise((resolve, reject) => {
      const script = documentOwner.createElement("script");
      script.src = url;
      script.onload = () => resolve();
      script.onerror = () => {
        classicScripts.delete(url);
        script.remove();
        reject(Error(`Could not load ${src}. Reload this tool to retry.`));
      };
      documentOwner.head.append(script);
    });
    classicScripts.set(url, pending);
  }
  return pending;
}
