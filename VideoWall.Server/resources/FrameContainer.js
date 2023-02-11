export class FrameContainer {
    constructor(id) {
        this.id = id;
    }

    get element() { return document.getElementById(this.id); }

    clear() { this.element.innerHTML = ""; }

    addFrame(frame) {
        const div = document.createElement("div");
        div.innerHTML = `
            <div>${frame.id}</div>
            <div>${frame.x}, ${frame.y}, ${frame.width}, ${frame.height}</div>
`
        this.element.appendChild(div);
    }
}