export class FrameApi {
    constructor(frameData) {
        this.frameData = frameData;
    }
    
    get id() { return this.frameData.id; }
    get x() { return this.frameData.location.x; }
    get y() { return this.frameData.location.y; }
    get width() { return this.frameData.location.width; }
    get height() { return this.frameData.location.height; }
}