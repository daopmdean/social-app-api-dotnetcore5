using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ServiceFilter(typeof(LogUserActivity))]
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly IMessageRepository _messageRepository;

        public MessageController(IUserRepository userRepository, IMapper mapper,
            IMessageRepository messageRepository)
        {
            _userRepository = userRepository;
            _mapper = mapper;
            _messageRepository = messageRepository;
        }

        [HttpPost]
        public async Task<ActionResult<MessageDto>> CreateMessage(CreateMessageDto createMessageDto)
        {
            var username = User.GetUserName();

            if (username == createMessageDto.ReceiverUsername.ToLower())
                return BadRequest("You can not send message to your self");

            var sender = await _userRepository.GetUserByUsernameAsync(username);
            var receiver = await _userRepository
                .GetUserByUsernameAsync(createMessageDto.ReceiverUsername);

            if (receiver == null)
                return NotFound();

            var message = new Message
            {
                Sender = sender,
                SenderUsername = sender.UserName,
                Receiver = receiver,
                ReceiverUsername = receiver.UserName,
                Content = createMessageDto.Content
            };

            _messageRepository.AddMessage(message);

            if (await _messageRepository.SaveAllAsync())
                return Ok(_mapper.Map<MessageDto>(message));

            return BadRequest("Failed to send message");
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MessageDto>>>
            GetMessagesForUser([FromQuery] MessageParams messageParams)
        {
            messageParams.Username = User.GetUserName();

            var messages = await _messageRepository.GetMessagesForUser(messageParams);

            Response.AddPaginationHeader(messages.CurrentPage, messages.PageSize,
                messages.TotalCount, messages.TotalPages);

            return messages;
        }

        [HttpGet("thread/{username}")]
        public async Task<ActionResult<IEnumerable<MessageDto>>>
            GetMessageThread(string username)
        {
            var currentUsername = User.GetUserName();

            return Ok(await _messageRepository.GetMessageThread(currentUsername, username));
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteMessage(int id)
        {
            var username = User.GetUserName();
            var message = await _messageRepository.GetMessage(id);

            if (message.Sender.UserName != username && message.Receiver.UserName != username)
                return Unauthorized();

            if (message.Sender.UserName == username)
            {
                message.SenderDeleted = true;
            }

            if (message.Receiver.UserName == username)
            {
                message.ReceiverDeleted = true;
            }

            if (message.SenderDeleted && message.ReceiverDeleted)
            {
                _messageRepository.DeleteMessage(message);
            }

            if (await _messageRepository.SaveAllAsync())
                return Ok();

            return BadRequest("Failed to delete message");
        }
    }
}
