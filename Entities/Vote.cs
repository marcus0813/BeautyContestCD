﻿namespace API.Entities
{
    public class Vote
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; }

        public string ImageUrl { get; set; }

        public long Price { get; set; }

    }
}

