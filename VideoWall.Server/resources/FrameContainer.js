export class FrameContainer {
    constructor(id) {
        this.id = id;
    }

    get element() { return document.getElementById(this.id); }
    get context() { return this.element.getContext("2d"); }

    clear() { this.context.clearRect(0, 0, this.element.width, this.element.height); }

    setSize(width, height) {
        this.element.width = width;
        this.element.height = height;
    }

    async addFrame(frame) {
        let img = new Image();
        await new Promise(r => {
            img.onload = r;
            img.src = frame.image;
        });
        this.context.drawImage(img, frame.x, frame.y, frame.width, frame.height);
    }
}