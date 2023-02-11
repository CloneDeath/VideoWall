import {FrameElement} from "./FrameElement.js";

export class FrameContainer {
    constructor(id) {
        this.id = id;
        this.frames = [];
    }

    get element() { return document.getElementById(this.id); }

    clear() { 
        this.frames = [];
    }

    setSize(width, height) {
        this.element.style.width = `${width}px`;
        this.element.style.height = `${height}px`;
    }

    addFrame(frame) {
        const frameElement = new FrameElement(frame);
        this.frames.push(frameElement);
        this.element.appendChild(frameElement.element);
    }
    
    async update() {
        for (const f of this.frames) {
            await f.update();
        }
    }
}