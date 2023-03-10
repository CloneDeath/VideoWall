import {FrameContainer} from "./FrameContainer.js";
import {Api} from "./Api/Api.js";

document.addEventListener('dragover', function(e) { e.preventDefault() })

const api = new Api();
const frames = await api.getFrames();

const frameContainer = new FrameContainer("frame-container");
frameContainer.setSize(800, 600);
frameContainer.clear();

frames.forEach(async f => {
    frameContainer.addFrame(f);
});

setInterval(async() => {
    await frameContainer.update();
}, 1000);