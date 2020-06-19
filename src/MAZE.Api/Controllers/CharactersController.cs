﻿using System;
using System.Linq;
using System.Threading.Tasks;
using MAZE.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc;
using CharacterId = System.Int32;
using GameId = System.String;
using LocationId = System.Int32;

namespace MAZE.Api.Controllers
{
    [Route("games/{gameId}/[controller]")]
    [ApiController]
    public class CharactersController : ControllerBase
    {
        private readonly CharacterService _characterService;

        public CharactersController(CharacterService characterService)
        {
            _characterService = characterService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(GameId gameId)
        {
            var result = await _characterService.GetCharactersAsync(gameId);

            return result.Map<IActionResult>(
                Ok,
                readGameError =>
                {
                    return readGameError switch
                    {
                        ReadGameError.NotFound => NotFound("Game not found"),
                        _ => throw new ArgumentOutOfRangeException(nameof(readGameError), readGameError, null)
                    };
                });
        }

        [HttpGet("{characterId}")]
        public async Task<IActionResult> Get(GameId gameId, CharacterId characterId)
        {
            var result = await _characterService.GetCharacterAsync(gameId, characterId);

            return result.Map<IActionResult>(
                Ok,
                readGameError =>
                {
                    return readGameError switch
                    {
                        ReadCharacterError.GameNotFound => NotFound("Game not found"),
                        ReadCharacterError.CharacterNotFound => NotFound("Character not found"),
                        _ => throw new ArgumentOutOfRangeException(nameof(readGameError), readGameError, null)
                    };
                });
        }

        [HttpPatch("{characterId}")]
        [Authorize]
        public async Task<IActionResult> Patch(GameId gameId, CharacterId characterId, JsonPatchDocument<Character> patch)
        {
            if (patch.Operations.Count != 1)
            {
                return BadRequest("Only one modification is currently supported");
            }

            var operation = patch.Operations.Single();

            if (operation.OperationType == OperationType.Replace &&
                operation.path.Equals(nameof(Character.Location), StringComparison.InvariantCultureIgnoreCase))
            {
                LocationId locationId;
                try
                {
                    locationId = Convert.ToInt32(operation.value);
                }
                catch
                {
                    return BadRequest("Invalid move to location");
                }

                var moveResult = await _characterService.MoveCharacterAsync(gameId, characterId, locationId);

                return await moveResult.Map<Task<IActionResult>>(
                    async () =>
                    {
                        var result = await _characterService.GetCharacterAsync(gameId, characterId);
                        return result.Map<IActionResult>(
                            Ok,
                            _ => Conflict("Character was unavailable after movement"));
                    },
                    moveCharacterError => Task.FromResult(CreateErrorResponse(moveCharacterError)));
            }
            else
            {
                return BadRequest("Unsupported operation");
            }
        }

        private IActionResult CreateErrorResponse(MoveCharacterError error)
        {
            return error switch
            {
                MoveCharacterError.GameNotFound => NotFound("Game not found"),
                MoveCharacterError.CharacterNotFound => NotFound("Character not found"),
                MoveCharacterError.LocationNotFound => NotFound("Location not found"),
                MoveCharacterError.NotAnAvailableMovement => BadRequest("Not an available movement"),
                _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
            };
        }
    }
}
