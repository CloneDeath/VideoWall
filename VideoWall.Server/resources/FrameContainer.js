export class FrameContainer {
    constructor(id) {
        this.id = id;
        this.frames = [];
    }

    get element() { return document.getElementById(this.id); }
    get context() { return this.element.getContext("2d"); }

    clear() { 
        this.frames = [];
    }

    setSize(width, height) {
        this.element.width = width;
        this.element.height = height;
    }

    addFrame(frame) {
        this.frames.push(frame);
    }
    
    async update() {
        for (const f of this.frames) {
            await f.update();
        }

        const bufferCanvas = document.createElement('canvas');
        bufferCanvas.width = this.element.width;
        bufferCanvas.height = this.element.height;
        const bufferContext = bufferCanvas.getContext('2d');
        
        for (const frame of this.frames) {
            let img = new Image();
            await new Promise(r => {
                img.onload = r;
                img.src = frame.image;
            });
            bufferContext.drawImage(img, frame.x, frame.y, frame.width, frame.height);
        }
        
        this.context.drawImage(bufferCanvas, 0, 0);
    }
}