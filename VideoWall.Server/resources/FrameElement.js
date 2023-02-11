export class FrameElement {
    constructor(frame) {
        this.frame = frame;
        this.element = document.createElement("canvas");
        this.element.style.position = "absolute";
    }

    get context() { return this.element.getContext("2d"); }
    
    async update() {
        await this.frame.update();
        
        const image = await this.getImage();

        this.element.style.left = `${this.frame.x}px`;
        this.element.style.top = `${this.frame.y}px`;
        this.element.width = this.frame.width;
        this.element.height = this.frame.height;
        this.context.drawImage(image, 0, 0);
    }
    
    async getImage() {
        const bufferCanvas = document.createElement('canvas');
        bufferCanvas.width = this.frame.width;
        bufferCanvas.height = this.frame.height;
        const bufferContext = bufferCanvas.getContext('2d');

        let img = new Image();
        await new Promise(r => {
            img.onload = r;
            img.src = this.frame.image;
        });
        bufferContext.drawImage(img, 0, 0, this.frame.width, this.frame.height);
        return bufferCanvas;
    }
}