using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MixTok.Core;

namespace MixTok.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class GetClipsController : ControllerBase
    {
        // GET api/getclips
        [HttpGet]
        public ActionResult<List<TokClip>> Get()
        {
            return Program.Mon.GetClips(500);
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(int id)
        {
            return "value";
        }
    }
}
