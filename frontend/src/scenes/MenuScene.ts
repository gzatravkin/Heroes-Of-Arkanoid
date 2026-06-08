export function mountMenu(host: HTMLElement) {
  const el = document.createElement("div");
  el.id = "menu";
  el.style.cssText = "color:#e8e8ff;font-family:sans-serif;text-align:center;padding-top:20vh";
  el.innerHTML = `<h1>ARKANOID RPG</h1>
    <button id="btn-play" style="font-size:20px;padding:10px 24px">Play (Hell I)</button>`;
  host.appendChild(el);
  document.getElementById("btn-play")!.addEventListener("click", () => {
    location.search = "?scene=battle&level=hell-1";
  });
}
