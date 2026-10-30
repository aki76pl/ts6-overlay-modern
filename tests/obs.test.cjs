const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const script = fs.readFileSync('ObsServer.cs', 'utf8').split('<script>')[1].split('</script>')[0];
let now = 0;
const root = {style: {setProperty() {}}, innerHTML:''};
const member = {id:1, uid:'one', channelId:10, nick:'Pilot <&>', talking:true};
const quiet = {id:2, uid:'two', channelId:10, nick:'Quiet', talking:false};
const state = {connected:true, minimal:false, horizontal:false, showChannelList:false, speakerFade:true, hold:2000, fade:500,
  theme:{Panel:'black',PanelBorder:'blue',Header:'white'}, radius:4, borderWidth:1, texts:{me:' (ty)'},
  servers:[{id:1,server:'Test',channel:'Channel',members:[member,quiet],watched:[]}],notices:[]};
const ctx = vm.createContext({URLSearchParams, location:{search:''}, document:{getElementById(){return root;}}, performance:{now:()=>now}, Date,
  fetch:async()=>({json:async()=>state}), setInterval() {}});
vm.runInContext(script,ctx);
async function tick(){await vm.runInContext('tick()',ctx);}
(async()=>{
  await tick(); assert.match(root.innerHTML,/Pilot &lt;&amp;&gt;/); assert.doesNotMatch(root.innerHTML,/Quiet/);
  now=100; member.talking=false; await tick(); assert.match(root.innerHTML,/opacity:1;/);
  now=2350; await tick(); assert.match(root.innerHTML,/opacity:0.5;/);
  now=2600; await tick(); assert.doesNotMatch(root.innerHTML,/Pilot/);
  member.talking=true; await tick(); assert.match(root.innerHTML,/opacity:1;/);
  state.minimal=true;state.horizontal=true; await tick(); assert.equal(root.style.display,'inline-flex');assert.equal(root.style.background,'transparent');assert.doesNotMatch(root.innerHTML,/class="hdr"/);
  member.talking=false;member.uid='new-user'; await tick();assert.doesNotMatch(root.innerHTML,/Pilot/);
  state.showChannelList=true;await tick();assert.match(root.innerHTML,/Quiet/);
  state.showChannelList=false;state.connected=false;await tick();state.connected=true;await tick();assert.doesNotMatch(root.innerHTML,/Pilot/);
  console.log('PASS: OBS hold, fade, resume, identity, disconnect, minimal layout and escaping');
})().catch(e=>{console.error(e);process.exitCode=1;});
