export class FrameElement {
    constructor(frame) {
        this.frame = frame;
        this.element = document.createElement("canvas");
        this.element.classList.add("frame");
        this.element.draggable = true;
        this.element.addEventListener("dragstart", (event) => this.onDragStart(event));
        this.element.addEventListener("dragend", (event) => this.onDragEnd(event));
        this.newPosition = null;
        this.initialPosition = null;
    }

    get context() { return this.element.getContext("2d"); }
    
    async update() {
        await this.frame.update();
        if (this.newPosition != null) {
            if (this.frame.x === this.newPosition.x && this.frame.y === this.newPosition.y) {
                this.newPosition = null;
            } else {
                await this.frame.updatePosition(this.newPosition.x, this.newPosition.y);
            }
        }
        
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
    
    onDragStart(event) {
        this.initialPosition = {x: event.x, y: event.y};
    }
    
    onDragEnd(event) {
        const dx = event.x - this.initialPosition.x;
        const dy = event.y - this.initialPosition.y;
        this.newPosition = {x: this.frame.x + dx, y: this.frame.y + dy};
        this.element.style.left = `${this.newPosition.x}px`;
        this.element.style.top = `${this.newPosition.y}px`;
    }
}